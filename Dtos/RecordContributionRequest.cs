// What a caller sends to record a member's contribution for a specific cycle.
// This record's value-equality is what powers the idempotency check.
namespace RondiTrack.Dtos;

public record RecordContributionRequest(Guid UserId, string Cycle, decimal Amount);