using RondiTrack.Domain;

namespace RondiTrack.Dtos;

// Every filter arrives as text so a bad value is always a 400 from our own validation, never a
// binding error that looks different. From and To are UTC instants: From is included, To is excluded.
public record HistoryQuery(int? PageSize, string? PageToken, string? Member, string? From, string? To, string? Sort);

public record HistoryItemResponse(
    Guid Id,
    Guid MemberId,
    string MemberName,
    long AmountCents,
    ContributionStatus Status,
    DateTime RecordedAt,   // UTC instant
    Guid CycleId,
    string CycleLabel);

public record ActivityQuery(int? PageSize, string? PageToken);

// Kind is Contribution, Payout or MemberJoined. ActorName is null unless the caller is a Treasurer
// or Admin of that stokvel, or the actor is the caller. AmountCents is null for MemberJoined.
public record ActivityItemResponse(
    Guid Id,
    string Kind,
    Guid StokvelId,
    string StokvelName,
    DateTime OccurredAt,   // UTC instant
    string? ActorName,
    long? AmountCents);