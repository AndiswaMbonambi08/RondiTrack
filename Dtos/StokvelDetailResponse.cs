// What a member sees on the stokvel detail page. Money is integer cents.
// Nothing here identifies another member: only counts and one total for the current cycle.
namespace RondiTrack.Dtos;

public record StokvelDetailResponse(
    Guid Id,
    string Name,
    long ContributionCents,
    string Role,                           // the CALLER's role in this stokvel: "Member", "Treasurer" or "Admin"
    int MemberCount,
    StokvelCycleSummary? CurrentCycle);    // null when the stokvel has no open cycle

public record StokvelCycleSummary(
    Guid Id,
    string Label,
    string Status,                         // "Open" or "Closed"
    DateOnly? DueDate,
    int ContributorCount,                  // how many members have paid this cycle
    long CollectedCents);                  // total paid this cycle, all members together
