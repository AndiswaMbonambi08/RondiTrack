// What a caller receives back after a contribution is recorded.
namespace RondiTrack.Dtos;

public record ContributionResponse(Guid Id, Guid StokvelId, Guid UserId, Guid ContributionCycleId, decimal Amount, DateTime RecordedAt);