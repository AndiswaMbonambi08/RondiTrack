// A single member's payment toward one ContributionCycle. Carries StokvelId directly,
// so it has a real composite FK to StokvelMember: (StokvelId, UserId) is the same pair
// that identifies the StokvelMember row itself.
using System.Text.Json.Serialization;

namespace RondiTrack.Domain;

// Published to OpenAPI as a string enum, so the generated TypeScript type is a union.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContributionStatus
{
    Pending,
    Paid,
    Confirmed
}

public class Contribution
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public ContributionStatus Status { get; private set; }

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
        Status = ContributionStatus.Paid;   // a recorded contribution starts as Paid
    }

    // Used by the seed, so demo data can have a fixed instant and status.
    public Contribution(Guid stokvelId, Guid userId, Guid contributionCycleId, decimal amount,
                        DateTime recordedAtUtc, ContributionStatus status)
        : this(stokvelId, userId, contributionCycleId, amount)
    {
        RecordedAt = DateTime.SpecifyKind(recordedAtUtc, DateTimeKind.Utc);
        Status = status;
    }
}