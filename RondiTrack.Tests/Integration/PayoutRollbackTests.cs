// Forces a failure partway through payout processing, then re-queries the
// database directly to prove neither write (the Payout, or the cycle status
// change) was left behind. Asserts by re-querying, not by checking a status code.
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;
using Xunit;

namespace RondiTrack.Tests.Integration;

public class PayoutRollbackTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PayoutRollbackTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessPayoutAsync_FailureBetweenWrites_LeavesNoPartialData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var stokvelRepo = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var cycleRepo = scope.ServiceProvider.GetRequiredService<IContributionCycleRepository>();
        var payoutRepo = scope.ServiceProvider.GetRequiredService<IPayoutRepository>();

        var user = new User("Rollback Test User", $"{Guid.NewGuid()}@example.com");
        await userRepo.AddAsync(user);

        var stokvel = new Stokvel("Rollback Test Stokvel", 100m);
        stokvel.AddMember(user);
        await stokvelRepo.AddAsync(stokvel);
        db.StokvelMembers.Add(new RondiTrack.Persistence.Entities.StokvelMember(
        stokvel.Id, user.Id, RondiTrack.Persistence.Entities.StokvelMemberRole.Member, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var cycle = new ContributionCycle(stokvel.Id, "2026-10", 100m);
        await cycleRepo.AddAsync(cycle);

        var service = new PayoutService(db, stokvelRepo, cycleRepo, payoutRepo)
        {
            TestHookBeforeCycleUpdate = () => throw new InvalidOperationException("Forced failure for rollback test.")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessPayoutAsync(stokvel.Id, cycle.Id));

        // Re-query fresh from the database, not from anything still in memory,
        // to prove the transaction actually rolled back at the database level.
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        var payoutExists = verifyDb.Payouts.Any(p => p.ContributionCycleId == cycle.Id);
        var reloadedCycle = await verifyDb.ContributionCycles.FindAsync(cycle.Id);

        Assert.False(payoutExists);
        Assert.Equal(CycleStatus.Open, reloadedCycle!.Status);
    }
}