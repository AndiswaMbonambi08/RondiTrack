// One row of "my contributions in this stokvel". Money is integer cents so the client never rounds.
// No user id and nobody else's data: a caller only ever sees their own contributions.
using RondiTrack.Domain;

namespace RondiTrack.Dtos;

public record ContributionItemResponse(
    Guid Id,
    long AmountCents,
    ContributionStatus Status,
    DateTime RecordedAt,      // UTC instant
    Guid CycleId,
    string CycleLabel);

public record MyContributionListQuery(int? PageSize, string? PageToken);