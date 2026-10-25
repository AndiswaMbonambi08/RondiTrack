// Maps every /api/v1/stokvels route, including nested members and contributions,
// annotated with OpenAPI summaries, descriptions, and every realistic response type.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Authorization;
using RondiTrack.Browser;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Services;
using RondiTrack.Validation;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Endpoints;

public static class StokvelEndpoints
{
    public static void MapStokvelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Stokvels).WithTags("Stokvels");

        // The one plain role check in the project: a platform-level operation.
        group.MapGet("/", async (IStokvelRepository repo) =>
        {
            var stokvels = await repo.GetAllAsync();
            return Results.Ok(stokvels.Select(s => s.ToResponse()));
        })
        .RequireAuthorization(new AuthorizeAttribute { Roles = PlatformRoles.PlatformAdmin })
        .WithSummary("List all stokvels")
        .WithDescription("Platform admins only. Returns every stokvel with its member count.")
        .Produces<IEnumerable<StokvelResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
            StokvelRequest request,
            HttpContext http,
            IStokvelRepository repo,
            IUserRepository users,
            PendingMemberRoles pendingRoles) =>
        {
            var stokvel = new Stokvel(request.Name, request.ContributionAmount);

            // The creator becomes the stokvel's Admin. Platform admins are operators, so they stay
            // out of the membership.
            var creatorId = http.User.GetUserId();
            if (creatorId is not null && !http.User.IsInRole(PlatformRoles.PlatformAdmin))
            {
                var creator = await users.GetByIdAsync(creatorId.Value)
                    ?? throw new NotFoundException("User not found.");
                stokvel.AddMember(creator);
                pendingRoles.Set(stokvel.Id, creator.Id, StokvelMemberRole.Admin);
            }

            await repo.AddAsync(stokvel);
            return Results.Created($"{ApiRoutes.Stokvels}/{stokvel.Id}", stokvel.ToResponse());
        })
        .WithSummary("Create a stokvel")
        .WithDescription("""
            Any signed-in user may create a stokvel and becomes its Admin.
            contributionAmount must be strictly greater than zero.
            Example request:
            { "name": "Holiday Savings", "contributionAmount": 500 }
            """)
        .Produces<StokvelResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<StokvelRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, StokvelRequest request, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            stokvel.UpdateDetails(request.Name, request.ContributionAmount);
            return Results.Ok(stokvel.ToResponse());
        })
        .RequireAuthorization(Policies.StokvelAdmin)
        .WithSummary("Update a stokvel's name and contribution amount")
        .WithDescription("Stokvel Admin only. Does not affect existing members or past contributions.")
        .Produces<StokvelResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<StokvelRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.StokvelAdmin)
        .WithSummary("Delete a stokvel")
        .WithDescription("Stokvel Admin only. Does not delete related ContributionCycles; they remain orphaned. See the README's Definition of Done table.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, IStokvelService service) =>
        {
            var response = await service.AddMemberAsync(id, request.UserId);
            return Results.Created($"{ApiRoutes.Stokvels}/{id}", response);
        })
        .RequireAuthorization(Policies.StokvelAdmin)
        .WithSummary("Add a user as a stokvel member")
        .WithDescription("""
            Stokvel Admin only. Fails if the stokvel or user does not exist (404), if the user is already
            a member (409), or if the user is inactive (409). New members join with the Member role.
            Example request:
            { "userId": "..." }
            """)
        .Produces<StokvelResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .AddEndpointFilter<ValidationFilter<AddMemberRequest>>();

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            stokvel.RemoveMember(userId);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.StokvelAdmin)
        .WithSummary("Remove a member from a stokvel")
        .WithDescription("Stokvel Admin only. 404 if the stokvel does not exist, or if the given user is not currently a member.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/contributions", async (
            Guid id,
            RecordContributionRequest request,
            HttpContext http,
            IAuthorizationService authorization,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IStokvelService service) =>
        {
            // Second, resource-based check: the route policy proved membership, this one depends on
            // the request body. Members record their own contribution, Treasurer or Admin anyone's.
            var allowed = await authorization.AuthorizeAsync(
                http.User, new ContributionTarget(id, request.UserId), Policies.RecordContribution);
            if (!allowed.Succeeded)
                return AuthProblems.Forbidden(http);

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new RequestValidationException("An Idempotency-Key header is required to record a contribution.");

            // The key is scoped to the signed-in caller, so two users can pick the same key safely.
            var callerId = http.User.GetUserId() ?? request.UserId;
            var (response, replayed) = await service.RecordContributionWithReplayAsync(callerId, id, request, idempotencyKey);

            if (replayed)
                http.Response.Headers["Idempotency-Replayed"] = "true";

            return Results.Created($"{ApiRoutes.Stokvels}/{id}/contributions/{response.Id}", response);
        })
        .RequireAuthorization(Policies.StokvelMember)
        .RequireRateLimiting(RateLimiting.RecordContributionPolicy)   // token bucket per signed-in user
        .WithSummary("Record a member's contribution for a cycle")
        .WithDescription("""
            Members may record their own contribution. Treasurer or Admin may record anyone's (403 otherwise).
            amountInCents is a whole number of cents (50000 is R500.00), from 1 to 100 000 000.
            Requires an Idempotency-Key header, scoped to the caller and the stokvel. Repeating the same key
            with the same body returns the original response unchanged, with an Idempotency-Replayed: true
            header. Repeating the same key with a different body returns 422.
            Rate limited per user (429 with Retry-After). A 429 never consumes the key, so retry with the
            same key after the wait.
            Fails 404 if the stokvel, user, or contribution cycle does not exist, or if the cycle belongs
            to a different stokvel. Fails 409 if the user is not a member, or has already contributed for
            this cycle.
            Example request:
            { "userId": "...", "contributionCycleId": "...", "amountInCents": 50000 }
            """)
        .Produces<ContributionResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .AddEndpointFilter<ValidationFilter<RecordContributionRequest>>();
    }
}
