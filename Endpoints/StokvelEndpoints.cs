// Maps every /api/stokvels route, including nested members and contributions,
// annotated with OpenAPI summaries, descriptions, and every realistic response type.
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Services;
using RondiTrack.Validation;
using Microsoft.EntityFrameworkCore;


namespace RondiTrack.Endpoints;

public static class StokvelEndpoints
{
    public static void MapStokvelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stokvels").WithTags("Stokvels");

        group.MapGet("/", async (IStokvelRepository repo) =>
        {
            var stokvels = await repo.GetAllAsync();
            return Results.Ok(stokvels.Select(s => s.ToResponse()));
        })
        .WithSummary("List all stokvels")
        .WithDescription("Returns every stokvel with its member count. Never fails; an empty store returns an empty array.")
        .Produces<IEnumerable<StokvelResponse>>(StatusCodes.Status200OK);

     /*     group.MapGet("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");
            return Results.Ok(stokvel.ToResponse());
        })
        .WithSummary("Get a stokvel by id")
        .WithDescription("""
            Example response (200):
            { "id": "...", "name": "Ubuntu Savings Circle", "contributionAmount": 500, "memberCount": 2 }
            """)
        .Produces<StokvelResponse>(StatusCodes.Status200OK) 
        .ProducesProblem(StatusCodes.Status404NotFound); */

        group.MapPost("/", async (StokvelRequest request, IStokvelRepository repo) =>
        {
            var stokvel = new Stokvel(request.Name, request.ContributionAmount);
            await repo.AddAsync(stokvel);
            return Results.Created($"/api/stokvels/{stokvel.Id}", stokvel.ToResponse());
        })
        .WithSummary("Create a stokvel")
        .WithDescription("""
            contributionAmount must be strictly greater than zero.
            Example request:
            { "name": "Holiday Savings", "contributionAmount": 500 }
            Example response (201):
            { "id": "...", "name": "Holiday Savings", "contributionAmount": 500, "memberCount": 0 }
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
        .WithSummary("Update a stokvel's name and contribution amount")
        .WithDescription("Does not affect existing members or past contributions.")
        .Produces<StokvelResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<StokvelRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithSummary("Delete a stokvel")
        .WithDescription("Does not delete related ContributionCycles; they remain orphaned. See the README's Definition of Done table.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

/*         group.MapGet("/{id:guid}/members", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");
            return Results.Ok(stokvel.MemberIds);
        })
        .WithSummary("List a stokvel's member ids")
        .WithDescription("Returns an empty array for a stokvel with no members yet, not an error.")
        .Produces<IEnumerable<Guid>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound); */

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, IStokvelService service) =>
        {
            var response = await service.AddMemberAsync(id, request.UserId);
            return Results.Created($"/api/stokvels/{id}", response);
        })
        .WithSummary("Add a user as a stokvel member")
        .WithDescription("""
            Fails if the stokvel or user does not exist (404), if the user is already a member (409),
            or if the user is inactive (409).
            Example request:
            { "userId": "..." }
            """)
        .Produces<StokvelResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
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
        .WithSummary("Remove a member from a stokvel")
        .WithDescription("404 if the stokvel does not exist, or if the given user is not currently a member.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/contributions", async (
            Guid id,
            RecordContributionRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IStokvelService service) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new RequestValidationException("An Idempotency-Key header is required to record a contribution.");

            var response = await service.RecordContributionAsync(id, request, idempotencyKey);
            return Results.Created($"/api/stokvels/{id}/contributions/{response.Id}", response);
        })
        .WithSummary("Record a member's contribution for a cycle")
        .WithDescription("""
            Requires an Idempotency-Key header. Repeating the same key with the same body returns the
            original response unchanged. Repeating the same key with a different body returns 422.
            Fails 404 if the stokvel, user, or contribution cycle does not exist, or if the cycle belongs
            to a different stokvel. Fails 409 if the user is not a member, or has already contributed for
            this cycle.
            Example request:
            { "userId": "...", "contributionCycleId": "...", "amount": 500 }
            """)
        .Produces<ContributionResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .AddEndpointFilter<ValidationFilter<RecordContributionRequest>>();

          /*   group.MapGet("/{stokvelId:guid}/cycles/{cycleId:guid}/contributions", async (
            Guid stokvelId, Guid cycleId, RondiTrackDbContext db) =>
        {
            // NOT SHIPPED — eager loading, for comparison. One query (with
            // joins), but pulls every mapped column of Contribution,
            // StokvelMember, and User even though the response only needs
            // UserFullName out of all of that.
            //
            // var eager = await db.Contributions
            //     .Where(c => c.StokvelId == stokvelId && c.ContributionCycleId == cycleId)
            //     .Include(c => c.Member).ThenInclude(m => m!.User)
            //     .AsNoTracking()
            //     .ToListAsync();

            // SHIPPED — projection. Also one query, but selects only the five
            // columns the response actually returns. At 5 members the
            // difference is a handful of wasted columns; at 50 members
            // returning every contribution in a cycle, eager loading would
            // materialize 50 full User rows (email, isActive, everything)
            // and 50 full StokvelMember rows (role, joinedAtUtc) that the
            // response throws away immediately. Projection scales with what's
            // actually returned, not with the full object graph.
            var results = await db.Contributions
                .Where(c => c.StokvelId == stokvelId && c.ContributionCycleId == cycleId)
                .Select(c => new ContributionDetailResponse(
                    c.Id,
                    c.UserId,
                    c.Member!.User!.FullName,
                    c.Amount,
                    c.RecordedAt))
                .ToListAsync();

            return Results.Ok(results);
        })
        .WithSummary("List a cycle's contributions with contributor names")
        .WithDescription("Uses a projection query — fetches only the columns this response returns, not the full entity graph.")
        .Produces<IEnumerable<ContributionDetailResponse>>(StatusCodes.Status200OK);
    } */
    }
}