// Hand-written mapping from the ContributionCycle entity to its response DTO.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class ContributionCycleMapping
{
    public static ContributionCycleResponse ToResponse(this ContributionCycle cycle)
        => new(cycle.Id, cycle.StokvelId, cycle.Label, cycle.TargetAmount);
}