using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Tests.Integration;

// Small hand-written fake: the Microsoft.Extensions.TimeProvider.Testing package is a new NuGet package.
file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

[Collection("Postgres collection")]
public class HomeEndpointTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;
    private readonly WebApplicationFactory<Program> _app;

    public HomeEndpointTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
        _app = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNow))));
    }

    public void Dispose() => _app.Dispose();

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>())!.Id;
    }

    private async Task<Guid> CreateStokvelAsync(string name)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name, contributionAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<StokvelResponse>())!.Id;
    }

    private async Task AddMemberAsync(Guid stokvelId, Guid userId, StokvelMemberRole role)
    {
        var res = await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId });
        res.EnsureSuccessStatusCode();

        if (role == StokvelMemberRole.Member) return;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var row = await db.StokvelMembers.SingleAsync(m => m.StokvelId == stokvelId && m.UserId == userId);
        db.Entry(row).Property(m => m.Role).CurrentValue = role;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateCycleAsync(Guid stokvelId, string label)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label, targetAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ContributionCycleResponse>())!.Id;
    }

    private HttpClient ClientFor(Guid userId)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _factory.MintToken(userId));
        return client;
    }

    [Fact]
    public async Task MemberOfTwoStokvels_SeesBothWithRightRole_PaidAndOwed()
    {
        var userId = await CreateUserAsync();

        var stokvelA = await CreateStokvelAsync("Home Stokvel A");
        var stokvelB = await CreateStokvelAsync("Home Stokvel B");
        await AddMemberAsync(stokvelA, userId, StokvelMemberRole.Member);
        await AddMemberAsync(stokvelB, userId, StokvelMemberRole.Treasurer);

        var cycleA = await CreateCycleAsync(stokvelA, "2026-09");
        var cycleB = await CreateCycleAsync(stokvelB, "2026-10");

        // The user pays in stokvel A only.
        var client = ClientFor(userId);
        var pay = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelA}/contributions")
        {
            Content = JsonContent.Create(new { userId, contributionCycleId = cycleA, amount = 500 })
        };
        pay.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(pay)).StatusCode);

        var res = await client.GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var home = (await res.Content.ReadFromJsonAsync<HomeResponse>())!;

        Assert.Equal(FixedNow.UtcDateTime, home.AsOfUtc);
        Assert.Equal(2, home.Stokvels.Count);

        var a = home.Stokvels.Single(s => s.StokvelId == stokvelA);
        Assert.Equal("Member", a.Role);
        Assert.Equal(cycleA, a.CurrentCycle!.Id);
        Assert.Equal("Open", a.CurrentCycle.Status);
        Assert.Null(a.CurrentCycle.DueDate);   // cycles created through the API have no dates
        Assert.Equal(50000, a.PaidCents);
        Assert.Equal(0, a.OwedCents);
        // The last moment of September in Johannesburg (23:59:59 SAST) is 21:59:59Z.
        Assert.Equal(new DateTime(2026, 9, 30, 21, 59, 59, DateTimeKind.Utc), a.NextPayoutUtc);

        var b = home.Stokvels.Single(s => s.StokvelId == stokvelB);
        Assert.Equal("Treasurer", b.Role);
        Assert.Equal(cycleB, b.CurrentCycle!.Id);
        Assert.Equal(0, b.PaidCents);
        Assert.Equal(50000, b.OwedCents);
        Assert.Equal(new DateTime(2026, 10, 31, 21, 59, 59, DateTimeKind.Utc), b.NextPayoutUtc);
    }

    [Fact]
    public async Task UserInNoStokvels_Gets200WithEmptyList()
    {
        var userId = await CreateUserAsync();

        var res = await ClientFor(userId).GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var home = (await res.Content.ReadFromJsonAsync<HomeResponse>())!;
        Assert.Empty(home.Stokvels);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        var res = await _factory.CreateAnonymousClient().GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Response_ContainsNoMemberIds()
    {
        var userId = await CreateUserAsync();
        var other = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Privacy Stokvel");
        await AddMemberAsync(stokvel, userId, StokvelMemberRole.Member);
        await AddMemberAsync(stokvel, other, StokvelMemberRole.Member);
        await CreateCycleAsync(stokvel, "2026-10");

        var body = await (await ClientFor(userId).GetAsync("/api/v1/me/home")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(other.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(userId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }
}