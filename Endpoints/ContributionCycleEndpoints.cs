// Maps every /api/contribution-cycles route. Straight repository access from the
// endpoint, no service layer, because creating or updating a cycle involves no
// cross-entity decision — just "is this input valid," which validation already covers.
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
        });

        group.MapGet("/{id:guid}", async (Guid id, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Contribution cycle not found.");
            return Results.Ok(cycle.ToResponse());
        });

        group.MapPost("/", async (ContributionCycleRequest request, IStokvelRepository stokvelRepo, IContributionCycleRepository repo) =>
        {
            _ = await stokvelRepo.GetByIdAsync(request.StokvelId)
                ?? throw new NotFoundException("Stokvel not found.");

            var cycle = new ContributionCycle(request.StokvelId, request.Label, request.TargetAmount);
            await repo.AddAsync(cycle);
            return Results.Created($"/api/contribution-cycles/{cycle.Id}", cycle.ToResponse());
        }).AddEndpointFilter<ValidationFilter<ContributionCycleRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, ContributionCycleRequest request, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Contribution cycle not found.");

            cycle.UpdateDetails(request.Label, request.TargetAmount);
            return Results.Ok(cycle.ToResponse());
        }).AddEndpointFilter<ValidationFilter<ContributionCycleRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IContributionCycleRepository repo) =>
        {
            var cycle = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Contribution cycle not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        });
    }
}