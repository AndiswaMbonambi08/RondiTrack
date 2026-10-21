// What a caller receives back after a contribution is recorded.
// Amount is rand for older clients; AmountCents is the exact integer the web uses.
using RondiTrack.Domain;

namespace RondiTrack.Dtos;

public record ContributionResponse(
    Guid Id, Guid StokvelId, Guid UserId, Guid ContributionCycleId,
    decimal Amount, DateTime RecordedAt,
    long AmountCents, ContributionStatus Status);