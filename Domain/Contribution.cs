// A single member's payment toward one stokvel cycle. Validates its own amount and cycle
// label so an invalid contribution can never be constructed.
namespace RondiTrack.Domain;

public class Contribution
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public string Cycle { get; }
    public decimal Amount { get; }
    public DateTime RecordedAt { get; }

    public Contribution(Guid userId, string cycle, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(cycle))
            throw new ArgumentException("Cycle is required.", nameof(cycle));
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        UserId = userId;
        Cycle = cycle.Trim();
        Amount = amount;
        RecordedAt = DateTime.UtcNow;
    }
}