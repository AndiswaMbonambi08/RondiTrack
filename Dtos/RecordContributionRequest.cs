// What a caller sends to record a member's contribution against a real ContributionCycle.
namespace RondiTrack.Dtos;

public record RecordContributionRequest(Guid UserId, Guid ContributionCycleId, decimal Amount);