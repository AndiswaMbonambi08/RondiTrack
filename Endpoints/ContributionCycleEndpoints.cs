// Maps every /api/contribution-cycles route, annotated with OpenAPI metadata.
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Validation;

namespace RondiTrack.Endpoints;

public static class ContributionCycleEndpoints
{
    // The stokvel id is not in these URLs, so each handler looks it up and runs the stokvel policy.
    // A non-member gets the same 404 text as a cycle that does not exist.
    private const string CycleNotFound = "Contribution cycle not found.";

    public static void MapContributionCycleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/contribution-cycles").WithTags("ContributionCycles");

        group.MapGet("/", async (HttpContext http, IContributionCycleRepository repo, IStokvelMembershipLookup membership) =>
        {
            IEnumerable<ContributionCycle> visible = await repo.GetAllAsync();

            if (!http.User.IsInRole(PlatformRoles.PlatformAdmin))
            {
                var userId = http.User.GetUserId();
                var stokvelIds = userId is null
                    ? new List<Guid>()
                    : await membership.GetStokvelIdsAsync(userId.Value, http.RequestAborted);
                visible = visible.Where(c => stokvelIds.Contains(c.StokvelId));
            }

            return Results.Ok(visible.Select(c => c.ToResponse()));
        })
        .WithSummary("List contribution cycles")
        .WithDescription("Returns the cycles of every stokvel the caller belongs to. Platform admins see every cycle.")
        .Produces<IEnumerable<ContributionCycleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, StokvelAccess access, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException(CycleNotFound);

            var denied = await access.DenyAsync(http, cycle.StokvelId, Policies.StokvelMember, CycleNotFound);
            if (denied is not null) return denied;

            return Results.Ok(cycle.ToResponse());
        })
        .WithSummary("Get a contribution cycle by id")
        .Produces<ContributionCycleResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            ContributionCycleRequest request,
            HttpContext http,
            StokvelAccess access,
            IStokvelRepository stokvelRepo,
            IContributionCycleRepository repo) =>
        {
            // Authorize before looking the stokvel up, so a non-member cannot tell whether it exists.
            var denied = await access.DenyAsync(http, request.StokvelId, Policies.StokvelTreasurer);
            if (denied is not null) return denied;

            _ = await stokvelRepo.GetByIdAsync(request.StokvelId)
                ?? throw new NotFoundException("Stokvel not found.");

            var cycle = new ContributionCycle(request.StokvelId, request.Label, request.TargetAmount);
            await repo.AddAsync(cycle);
            return Results.Created($"/api/contribution-cycles/{cycle.Id}", cycle.ToResponse());
        })
        .WithSummary("Create a contribution cycle for a stokvel")
        .WithDescription("""
            Treasurer or Admin of the stokvel only. 404 if stokvelId does not refer to a stokvel the caller
            belongs to. targetAmount must be greater than zero.
            Example request:
            { "stokvelId": "...", "label": "2026-09", "targetAmount": 500 }
            """)
        .Produces<ContributionCycleResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<ContributionCycleRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            ContributionCycleRequest request,
            HttpContext http,
            StokvelAccess access,
            IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException(CycleNotFound);

            var denied = await access.DenyAsync(http, cycle.StokvelId, Policies.StokvelTreasurer, CycleNotFound);
            if (denied is not null) return denied;

            cycle.UpdateDetails(request.Label, request.TargetAmount);
            return Results.Ok(cycle.ToResponse());
        })
        .WithSummary("Update a contribution cycle's label and target amount")
        .WithDescription("Treasurer or Admin of the stokvel only. Does not allow moving a cycle to a different stokvel; stokvelId in the body is ignored on update.")
        .Produces<ContributionCycleResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<ContributionCycleRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, StokvelAccess access, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException(CycleNotFound);

            var denied = await access.DenyAsync(http, cycle.StokvelId, Policies.StokvelAdmin, CycleNotFound);
            if (denied is not null) return denied;

            await repo.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithSummary("Delete a contribution cycle")
        .WithDescription("Stokvel Admin only. Does not check for existing contributions recorded against this cycle. See the README's Definition of Done table.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}