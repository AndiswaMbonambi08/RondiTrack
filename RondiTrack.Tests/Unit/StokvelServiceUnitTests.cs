// Unit tests against StokvelService directly, using real in-memory repositories.
// No DI container, no HTTP. Proves the idempotency-key comparison logic, the
// cycle-belongs-to-a-different-stokvel cross-check, and the exact cents-to-rands conversion.
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Services;
using Xunit;

namespace RondiTrack.Tests.Unit;

public class StokvelServiceUnitTests
{
    private static (StokvelService Service, IStokvelRepository StokvelRepo, User User, Stokvel Stokvel, ContributionCycle Cycle) BuildScenario()
    {
        var userRepo = new InMemoryUserRepository();
        var stokvelRepo = new InMemoryStokvelRepository();
        var cycleRepo = new InMemoryContributionCycleRepository();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new StokvelService(stokvelRepo, userRepo, cycleRepo, idempotencyStore);

        var user = new User("Test User", "test@example.com");
        userRepo.AddAsync(user).Wait();

        var stokvel = new Stokvel("Test Stokvel", 100m);
        stokvel.AddMember(user);
        stokvelRepo.AddAsync(stokvel).Wait();

        var cycle = new ContributionCycle(stokvel.Id, "2026-09", 100m);
        cycleRepo.AddAsync(cycle).Wait();

        return (service, stokvelRepo, user, stokvel, cycle);
    }

    [Fact]
    public async Task RecordContributionAsync_SameKeySameRequest_ReturnsSameContributionIdBothTimes()
    {
        var (service, _, user, stokvel, cycle) = BuildScenario();
        var request = new RecordContributionRequest(user.Id, cycle.Id, 10000);

        var first = await service.RecordContributionAsync(stokvel.Id, request, "key-1");
        var second = await service.RecordContributionAsync(stokvel.Id, request, "key-1");

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task RecordContributionAsync_SameKeyDifferentAmount_ThrowsIdempotencyConflictException()
    {
        var (service, _, user, stokvel, cycle) = BuildScenario();
        var first = new RecordContributionRequest(user.Id, cycle.Id, 10000);
        var second = new RecordContributionRequest(user.Id, cycle.Id, 20000);

        await service.RecordContributionAsync(stokvel.Id, first, "key-1");

        await Assert.ThrowsAsync<IdempotencyConflictException>(
            () => service.RecordContributionAsync(stokvel.Id, second, "key-1"));
    }

    [Fact]
    public async Task RecordContributionAsync_CycleBelongsToDifferentStokvel_ThrowsNotFoundException()
    {
        var (service, stokvelRepo, user, stokvel, cycle) = BuildScenario();

        var otherStokvel = new Stokvel("Other Stokvel", 100m);
        otherStokvel.AddMember(user);
        await stokvelRepo.AddAsync(otherStokvel);

        var request = new RecordContributionRequest(user.Id, cycle.Id, 10000);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.RecordContributionAsync(otherStokvel.Id, request, "key-1"));
    }

    [Theory]
    [InlineData(15050L, 150.50)]
    [InlineData(1999L, 19.99)]
    [InlineData(1L, 0.01)]
    [InlineData(100_000_000L, 1_000_000.00)]
    public async Task RecordContributionAsync_ConvertsCentsToRandsExactly(long cents, double rands)
    {
        var (service, _, user, stokvel, cycle) = BuildScenario();

        var response = await service.RecordContributionAsync(
            stokvel.Id, new RecordContributionRequest(user.Id, cycle.Id, cents), $"key-{cents}");

        Assert.Equal(cents, response.AmountCents);
        Assert.Equal((decimal)rands, response.Amount);
    }
}
