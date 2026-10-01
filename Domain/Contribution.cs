// A single member's payment toward one ContributionCycle. Validates its own amount
// so an invalid contribution can never be constructed.
namespace RondiTrack.Domain;

public class Contribution
{
    public Guid UserId { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime RecordedAt { get; private set; }

    // EF Core needs a constructor it can call without arguments when loading
    // rows back from the database. The public constructor above stays the
    // only way application code creates a Contribution; this one is only
    // ever used by EF's internal materialization.
    private Contribution() { }

    public Contribution(Guid userId, Guid contributionCycleId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        UserId = userId;
        ContributionCycleId = contributionCycleId;
        Amount = amount;
        RecordedAt = DateTime.UtcNow;
    }
}