// A specific collection period for a stokvel: a target amount, a label identifying
// the period (e.g. "2026-09") and, for scheduled cycles, a start and end date.
// Dates are calendar dates in the stokvel's time zone (Africa/Johannesburg), NOT instants.
// The cycle-close job decides when a cycle has ended by converting EndDate to a UTC
// instant (see CloseInstantUtc) and comparing it with TimeProvider.
// Only validates its own shape, same as User and Stokvel.
using RondiTrack.Domain.Exceptions;
namespace RondiTrack.Domain;

// A cycle starts Open. Once a payout has been processed for it, it's closed
// off so it can't be paid out twice.
public enum CycleStatus
{
    Open,
    PayoutProcessed
}

public class ContributionCycle
{
    public ICollection<Contribution> Contributions { get; private set; } = new List<Contribution>();
    public CycleStatus Status { get; private set; } = CycleStatus.Open;

    public Guid Id { get; }
    public Guid StokvelId { get; private set; }
    public string Label { get; private set; }
    public decimal TargetAmount { get; private set; }

    // Null only for cycles created before scheduling existed; the job ignores those.
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    // Set by the cycle-close job with a conditional UPDATE (WHERE ClosedAtUtc IS NULL).
    // Non-null means "closed by the job"; the database guarantees it happens once.
    public DateTime? ClosedAtUtc { get; private set; }

    // EF Core needs a constructor it can call without arguments when loading
    // rows back from the database.
    private ContributionCycle() { }

    // Kept so existing callers and tests still compile. Produces an unscheduled cycle.
    public ContributionCycle(Guid stokvelId, string label, decimal targetAmount)
    {
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        SetLabel(label);
        SetTargetAmount(targetAmount);
    }

    public ContributionCycle(Guid stokvelId, string label, decimal targetAmount, DateOnly startDate, DateOnly endDate)
        : this(stokvelId, label, targetAmount)
    {
        if (endDate < startDate)
            throw new ArgumentException("Cycle end date cannot be before its start date.", nameof(endDate));
        StartDate = startDate;
        EndDate = endDate;
    }

    public void UpdateDetails(string label, decimal targetAmount)
    {
        SetLabel(label);
        SetTargetAmount(targetAmount);
    }

    // The instant this cycle closes: midnight at the END of EndDate in the stokvel's
    // time zone. A cycle ending "on the 25th" closes at 00:00 on the 26th SAST,
    // which is 22:00 UTC on the 25th (South Africa is UTC+2, no daylight saving).
    // Uses only the supplied TimeZoneInfo, never the machine's local zone or clock.
    public DateTime? CloseInstantUtc(TimeZoneInfo stokvelTimeZone)
    {
        if (EndDate is null) return null;
        var localMidnightAfterEnd = EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue); // Kind = Unspecified
        return TimeZoneInfo.ConvertTimeToUtc(localMidnightAfterEnd, stokvelTimeZone);
    }

    // Called once, during payout processing, inside the transaction below.
    // Refuses to run twice so the same cycle can't be paid out more than once.
    public void MarkPayoutProcessed()
    {
        if (Status == CycleStatus.PayoutProcessed)
            throw new ConflictException("This cycle's payout has already been processed.");
        Status = CycleStatus.PayoutProcessed;
    }

    // Seed data only: gives demo data a cycle that the cycle-close job has already closed.
    public void MarkClosedForSeed(DateTime closedAtUtc) => ClosedAtUtc = closedAtUtc;

    private void SetLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Cycle label is required.", nameof(label));
        Label = label.Trim();
    }

    private void SetTargetAmount(decimal targetAmount)
    {
        if (targetAmount <= 0)
            throw new ArgumentException("Target amount must be greater than zero.", nameof(targetAmount));
        TargetAmount = targetAmount;
    }
}