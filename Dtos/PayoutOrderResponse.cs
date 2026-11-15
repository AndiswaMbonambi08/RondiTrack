namespace RondiTrack.Dtos;

// One person's place in the rotation. Status is exactly "Paid", "Next" or "Waiting".
// PayoutDate is a calendar date in Africa/Johannesburg, or null if not paid yet.
public record PayoutOrderEntryResponse(
    Guid MemberId,
    string MemberName,
    int Position,
    long AmountCents,
    DateOnly? PayoutDate,
    string Status);

public record PayoutOrderResponse(
    Guid StokvelId,
    Guid CycleId,
    IReadOnlyList<PayoutOrderEntryResponse> Items);