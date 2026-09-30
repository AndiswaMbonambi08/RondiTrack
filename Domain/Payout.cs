// One payout: this stokvel paid this member this amount, for this cycle.
// Brand new today, so it protects its own amount the same way every other
// entity in RondiTrack protects itself.
namespace RondiTrack.Domain;

public class Payout
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid ContributionCycleId { get; }
    public Guid RecipientUserId { get; }
    public decimal Amount { get; }
    public DateTime PayoutDate { get; }

    public Payout(Guid stokvelId, Guid contributionCycleId, Guid recipientUserId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payout amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        ContributionCycleId = contributionCycleId;
        RecipientUserId = recipientUserId;
        Amount = amount;
        PayoutDate = DateTime.UtcNow;
    }
}