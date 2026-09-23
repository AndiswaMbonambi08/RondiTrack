// What a caller receives back for a ContributionCycle.
namespace RondiTrack.Dtos;

public record ContributionCycleResponse(Guid Id, Guid StokvelId, string Label, decimal TargetAmount);