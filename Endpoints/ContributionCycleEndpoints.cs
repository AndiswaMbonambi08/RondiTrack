// Maps every /api/contribution-cycles route, annotated with OpenAPI metadata.
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Validation;

namespace RondiTrack.Endpoints;

public static class ContributionCycleEndpoints
{
    public static void MapContributionCycleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/contribution-cycles").WithTags("ContributionCycles");

        group.MapGet("/", async (IContributionCycleRepository repo) =>
        {
            var cycles = await repo.GetAllAsync();
            return Results.Ok(cycles.Select(c => c.ToResponse()));
        })
        .WithSummary("List all contribution cycles")
        .WithDescription("Returns every cycle across every stokvel. Never fails; an empty store returns an empty array.")
        .Produces<IEnumerable<ContributionCycleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Contribution cycle not found.");
            return Results.Ok(cycle.ToResponse());
        })
        .WithSummary("Get a contribution cycle by id")
        .Produces<ContributionCycleResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (ContributionCycleRequest request, IStokvelRepository stokvelRepo, IContributionCycleRepository repo) =>
        {
            _ = await stokvelRepo.GetByIdAsync(request.StokvelId)
                ?? throw new NotFoundException("Stokvel not found.");

            var cycle = new ContributionCycle(request.StokvelId, request.Label, request.TargetAmount);
            await repo.AddAsync(cycle);
            return Results.Created($"/api/contribution-cycles/{cycle.Id}", cycle.ToResponse());
        })
        .WithSummary("Create a contribution cycle for a stokvel")
        .WithDescription("""
            404 if stokvelId does not refer to an existing stokvel. targetAmount must be greater than zero.
            Example request:
            { "stokvelId": "...", "label": "2026-09", "targetAmount": 500 }
            """)
        .Produces<ContributionCycleResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<ContributionCycleRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, ContributionCycleRequest request, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Contribution cycle not found.");

            cycle.UpdateDetails(request.Label, request.TargetAmount);
            return Results.Ok(cycle.ToResponse());
        })
        .WithSummary("Update a contribution cycle's label and target amount")
        .WithDescription("Does not allow moving a cycle to a different stokvel; stokvelId in the body is ignored on update.")
        .Produces<ContributionCycleResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<ContributionCycleRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Contribution cycle not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithSummary("Delete a contribution cycle")
        .WithDescription("Does not check for existing contributions recorded against this cycle. See the README's Definition of Done table.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}