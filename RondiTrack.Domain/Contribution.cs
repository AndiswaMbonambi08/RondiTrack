// A single member's payment toward one ContributionCycle. Now carries StokvelId
// directly (not just via the cycle), so it has a real composite FK to
// StokvelMember — this is the concrete answer to "how does a Contribution
// reference a specific membership": through (StokvelId, UserId) together,
// the same pair that identifies the StokvelMember row itself.
namespace RondiTrack.Domain;

public class Contribution
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime RecordedAt { get; private set; }

    public RondiTrack.Persistence.Entities.StokvelMember? Member { get; private set; }
    public ContributionCycle? ContributionCycle { get; private set; }

    private Contribution() { }

    public Contribution(Guid stokvelId, Guid userId, Guid contributionCycleId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        ContributionCycleId = contributionCycleId;
        Amount = amount;
        RecordedAt = DateTime.UtcNow;
    }
}