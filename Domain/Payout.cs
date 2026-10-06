// One payout: this stokvel paid this member this amount, for this cycle.
// Brand new today, so it protects its own amount the same way every other
// entity in RondiTrack protects itself.
namespace RondiTrack.Domain;

public class Payout
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PayoutDate { get; private set; }

    // EF Core needs a constructor it can call without arguments when loading
    // rows back from the database. The public constructor below stays the
    // only way application code creates a Payout.
    private Payout() { }

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