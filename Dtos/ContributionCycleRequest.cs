// What a caller sends to create or update a ContributionCycle.
namespace RondiTrack.Dtos;

public record ContributionCycleRequest(Guid StokvelId, string Label, decimal TargetAmount);