// A single member's payment toward one ContributionCycle. Validates its own amount
// so an invalid contribution can never be constructed.
namespace RondiTrack.Domain;

public class Contribution
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public Guid ContributionCycleId { get; }
    public decimal Amount { get; }
    public DateTime RecordedAt { get; }

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