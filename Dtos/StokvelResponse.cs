// What a caller receives back for a Stokvel. Exposes MemberCount instead of the raw
// member ID list, since that's what's actually useful on the wire.
namespace RondiTrack.Dtos;

public record StokvelResponse(Guid Id, string Name, decimal ContributionAmount, int MemberCount);