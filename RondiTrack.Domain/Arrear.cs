// A member's unpaid shortfall for one closed cycle, stored in cents (long) so no
// floating-point or decimal rounding ever touches a financial record.
// One arrear per (cycle, user) is enforced by a unique index in the DbContext.
namespace RondiTrack.Domain;

public class Arrear
{
    public Guid Id { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public long ShortfallCents { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    private Arrear() { }

    public Arrear(Guid contributionCycleId, Guid stokvelId, Guid userId, long shortfallCents, DateTime recordedAtUtc)
    {
        if (shortfallCents <= 0)
            throw new ArgumentException("Shortfall must be greater than zero.", nameof(shortfallCents));

        Id = Guid.NewGuid();
        ContributionCycleId = contributionCycleId;
        StokvelId = stokvelId;
        UserId = userId;
        ShortfallCents = shortfallCents;
        RecordedAtUtc = recordedAtUtc;
    }
}