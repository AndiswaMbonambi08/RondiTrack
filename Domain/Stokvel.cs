//The Stokvel entity. Owns its member list privately, enforces the membership rules (no duplicates, no inactive users), validates the contribution amount.
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Domain;

public class Stokvel
{
    private readonly List<Guid> _memberIds = new();

    public Guid Id { get; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public IReadOnlyList<Guid> MemberIds => _memberIds.AsReadOnly();

    public Stokvel(string name, decimal contributionAmount)
    {
        Id = Guid.NewGuid();
        SetName(name);
        SetContributionAmount(contributionAmount);
    }

    public void UpdateDetails(string name, decimal contributionAmount)
    {
        SetName(name);
        SetContributionAmount(contributionAmount);
    }

    public void AddMember(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsActive)
            throw new InactiveUserException($"'{user.FullName}' is inactive and cannot join a stokvel.");

        if (_memberIds.Contains(user.Id))
            throw new DuplicateMemberException($"'{user.FullName}' is already a member of this stokvel.");

        _memberIds.Add(user.Id);
    }

    public void RemoveMember(Guid userId)
    {
        if (!_memberIds.Remove(userId))
            throw new MemberNotFoundException("That user is not a member of this stokvel.");
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stokvel name is required.", nameof(name));
        Name = name.Trim();
    }

    private void SetContributionAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));
        ContributionAmount = amount;
    }
}