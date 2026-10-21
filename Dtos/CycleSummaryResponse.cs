// One row of GET /api/v1/stokvels/{id}/cycles. Money is integer cents.
// CollectedCents is summed by the API; AmountDueCents is the cycle's target amount.
namespace RondiTrack.Dtos;

public record CycleSummaryResponse(
    Guid Id,
    string Label,
    string Status,          // "Open", "Closed" or "PaidOut"
    DateOnly? DueDate,      // the cycle's end date as a calendar date; null for undated cycles
    long CollectedCents,
    long AmountDueCents);
