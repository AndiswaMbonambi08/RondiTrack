// Unit tests against the Stokvel entity directly. No HTTP, no DI container.
// Proves the membership rule and the duplicate-contribution rule.
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using Xunit;

namespace RondiTrack.Tests.Unit;

public class StokvelUnitTests
{
    private static User ActiveUser(string name = "Test User") => new(name, "test@example.com");

    [Fact]
    public void AddMember_InactiveUser_ThrowsConflictException()
    {
        var stokvel = new Stokvel("Test Stokvel", 100m);
        var user = ActiveUser();
        user.Deactivate();

        Assert.Throws<ConflictException>(() => stokvel.AddMember(user));
    }

    [Fact]
    public void AddMember_UserAlreadyAMember_ThrowsConflictException()
    {
        var stokvel = new Stokvel("Test Stokvel", 100m);
        var user = ActiveUser();
        stokvel.AddMember(user);

        Assert.Throws<ConflictException>(() => stokvel.AddMember(user));
    }

    [Fact]
    public void AddMember_ActiveNewUser_AddsToMemberIds()
    {
        var stokvel = new Stokvel("Test Stokvel", 100m);
        var user = ActiveUser();

        stokvel.AddMember(user);

        Assert.Contains(user.Id, stokvel.MemberIds);
    }

    [Fact]
    public void RecordContribution_NonMember_ThrowsNotFoundException()
    {
        var stokvel = new Stokvel("Test Stokvel", 100m);
        var user = ActiveUser();

        Assert.Throws<NotFoundException>(() => stokvel.RecordContribution(user, Guid.NewGuid(), 100m));
    }

    [Fact]
    public void RecordContribution_SameUserSameCycleTwice_ThrowsConflictException()
    {
        var stokvel = new Stokvel("Test Stokvel", 100m);
        var user = ActiveUser();
        stokvel.AddMember(user);
        var cycleId = Guid.NewGuid();
        stokvel.RecordContribution(user, cycleId, 100m);

        Assert.Throws<ConflictException>(() => stokvel.RecordContribution(user, cycleId, 100m));
    }

    [Fact]
    public void RecordContribution_MemberDifferentCycles_Succeeds()
    {
        var stokvel = new Stokvel("Test Stokvel", 100m);
        var user = ActiveUser();
        stokvel.AddMember(user);

        var first = stokvel.RecordContribution(user, Guid.NewGuid(), 100m);
        var second = stokvel.RecordContribution(user, Guid.NewGuid(), 100m);

        Assert.NotEqual(first.Id, second.Id);
    }
}