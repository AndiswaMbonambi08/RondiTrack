using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using Xunit;
using RondiTrack.Domain;
using RondiTrack.Services;

namespace RondiTrack.Tests.Integration;

public class ConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ConcurrencyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

private async Task<Guid> SeedPayoutAsync()
{
    using var scope = _factory.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    var stokvelRepo = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var cycleRepo = scope.ServiceProvider.GetRequiredService<IContributionCycleRepository>();

    var user = new User(
        "Concurrency Test User",
        $"{Guid.NewGuid()}@example.com");

    await userRepo.AddAsync(user);

    var stokvel = new Stokvel("Concurrency Test Stokvel", 100m);
    stokvel.AddMember(user);
    await stokvelRepo.AddAsync(stokvel);

    db.StokvelMembers.Add(
        new RondiTrack.Persistence.Entities.StokvelMember(
            stokvel.Id,
            user.Id,
            RondiTrack.Persistence.Entities.StokvelMemberRole.Member,
            DateTime.UtcNow));

    await db.SaveChangesAsync();

    var cycle = new ContributionCycle(
        stokvel.Id,
        "2026-10",
        100m);

    await cycleRepo.AddAsync(cycle);

    var payout = new Payout(
        stokvel.Id,
        cycle.Id,
        user.Id,
        100m);

    db.Payouts.Add(payout);
    await db.SaveChangesAsync();

    return payout.Id;
}
    [Fact]
    public async Task TwoContexts_SecondSaveOfStalePayout_ThrowsConcurrencyException()
    {
        var id = await SeedPayoutAsync();

        // Two independent DbContexts are required. A single DbContext tracks one instance per key and always
        // holds the version it last saw, so it can never be stale relative to itself. A conflict needs two
        // separate views of the same row, each holding the version it loaded before the other one saved.
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();
        var ctx1 = scope1.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        var a = await ctx1.Payouts.SingleAsync(p => p.Id == id);
        var b = await ctx2.Payouts.SingleAsync(p => p.Id == id);

        ctx1.Entry(a).Property(p => p.Amount).CurrentValue = a.Amount + 10;
        await ctx1.SaveChangesAsync();                 // first writer wins; the row's xmin changes

        ctx2.Entry(b).Property(p => p.Amount).CurrentValue = b.Amount + 20;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task Put_WithStaleETag_Returns412ProblemJson()
    {
        var id = await SeedPayoutAsync();

        var etag = (await _client.GetAsync($"/api/payouts/{id}")).Headers.ETag!.Tag;

        var first = new HttpRequestMessage(HttpMethod.Put, $"/api/payouts/{id}") { Content = JsonContent.Create(new { amount = 150m }) };
        first.Headers.TryAddWithoutValidation("If-Match", etag);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(first)).StatusCode);

        var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/payouts/{id}") { Content = JsonContent.Create(new { amount = 175m }) };
        stale.Headers.TryAddWithoutValidation("If-Match", etag);          // same token, now stale
        var res = await _client.SendAsync(stale);

        Assert.Equal(HttpStatusCode.PreconditionFailed, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType!.MediaType);
    }
}