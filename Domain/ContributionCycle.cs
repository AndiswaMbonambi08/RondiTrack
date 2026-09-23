// A specific collection period for a stokvel — a target amount and a label identifying
// the period (e.g. "2026-09"). No cross-entity decision is needed to create or update
// one, so this entity is handled straight from the endpoint via its repository, with
// no service layer. Only validates its own shape, same as User and Stokvel.
namespace RondiTrack.Domain;

public class ContributionCycle
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public string Label { get; private set; }
    public decimal TargetAmount { get; private set; }

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