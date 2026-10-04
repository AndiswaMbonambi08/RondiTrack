// A specific collection period for a stokvel — a target amount and a label identifying
// the period (e.g. "2026-09"). No cross-entity decision is needed to create or update
// one, so this entity is handled straight from the endpoint via its repository, with
// no service layer. Only validates its own shape, same as User and Stokvel.
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
    public Guid StokvelId { get; }
    public string Label { get; private set; }
    public decimal TargetAmount { get; private set; }

    // EF Core needs a constructor it can call without arguments when loading
    // rows back from the database. The public constructor below stays the
    // only way application code creates a ContributionCycle.
    private ContributionCycle() { }

    public ContributionCycle(Guid stokvelId, string label, decimal targetAmount)
    {
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        SetLabel(label);
        SetTargetAmount(targetAmount);
    }

    public void UpdateDetails(string label, decimal targetAmount)
    {
        SetLabel(label);
        SetTargetAmount(targetAmount);
    }

    // Called once, during payout processing, inside the transaction below.
    // Refuses to run twice so the same cycle can't be paid out more than once.
    public void MarkPayoutProcessed()
    {
        if (Status == CycleStatus.PayoutProcessed)
            throw new ConflictException("This cycle's payout has already been processed.");
        Status = CycleStatus.PayoutProcessed;
    }

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