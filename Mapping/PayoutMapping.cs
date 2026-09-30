using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class PayoutMapping
{
    public static PayoutResponse ToResponse(this Payout payout)
        => new(payout.Id, payout.StokvelId, payout.ContributionCycleId, payout.RecipientUserId, payout.Amount, payout.PayoutDate);
}