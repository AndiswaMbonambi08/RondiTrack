// What a caller sends to record a member's contribution against a real ContributionCycle.
// Money is integer cents. The domain still stores rands as a decimal; StokvelService converts
// once, exactly (cents / 100m), so no rounding can happen on the way in.
namespace RondiTrack.Dtos;

public record RecordContributionRequest(Guid UserId, Guid ContributionCycleId, long AmountInCents);
