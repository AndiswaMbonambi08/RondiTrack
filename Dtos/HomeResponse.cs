// The signed-in user's home screen. Money is integer cents so the client never rounds.
// Nothing here identifies another member: no member ids and no other members' amounts.
namespace RondiTrack.Dtos;

public record HomeResponse(DateTime AsOfUtc, IReadOnlyList<HomeStokvelResponse> Stokvels);

public record HomeStokvelResponse(
    Guid StokvelId,
    string Name,
    string Role,                       // "Member", "Treasurer" or "Admin"
    HomeCycleResponse? CurrentCycle,   // null when the stokvel has no open cycle
    long PaidCents,                    // what the caller paid this cycle
    long OwedCents,                    // what the caller still owes this cycle
    DateTime? NextPayoutUtc);          // last moment of the cycle's month, UTC; null when there is no open cycle

public record HomeCycleResponse(Guid Id, string Label);