using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class CyclesAndArrearsTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public CyclesAndArrearsTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
    }

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task<Guid> CreateStokvelAsync(string name)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name, contributionAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task AddMemberAsync(Guid stokvelId, Guid userId)
    {
        var res = await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId });
        res.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateCycleAsync(Guid stokvelId, string label)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label, targetAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private HttpClient ClientFor(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.MintToken(userId));
        return client;
    }

    [Fact]
    public async Task Home_ReturnsCallersOwnArrearsOnly()
    {
        var me = await CreateUserAsync();
        var other = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Arrears Stokvel");
        await AddMemberAsync(stokvel, me);
        await AddMemberAsync(stokvel, other);
        var cycle = await CreateCycleAsync(stokvel, "2026-10");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.Arrears.Add(new Arrear(cycle, stokvel, me, 15000, DateTime.UtcNow));
            db.Arrears.Add(new Arrear(cycle, stokvel, other, 99900, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var res = await ClientFor(me).GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var home = (await res.Content.ReadFromJsonAsync<HomeResponse>(JsonOptions.CaseInsensitive))!;

        Assert.Equal(15000, home.Stokvels.Single().ArrearsCents);
    }

    [Fact]
    public async Task Home_WithNoArrears_ReturnsZero()
    {
        var me = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Clean Stokvel");
        await AddMemberAsync(stokvel, me);
        await CreateCycleAsync(stokvel, "2026-10");

        var home = (await (await ClientFor(me).GetAsync("/api/v1/me/home"))
            .Content.ReadFromJsonAsync<HomeResponse>(JsonOptions.CaseInsensitive))!;

        Assert.Equal(0, home.Stokvels.Single().ArrearsCents);
    }

    [Fact]
    public async Task Cycles_ReturnStatusDueDateAndTotals_NewestFirst()
    {
        var me = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Cycles Stokvel");
        await AddMemberAsync(stokvel, me);

        Guid openId, closedId, paidOutId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

            var open = new ContributionCycle(stokvel, "2026-10", 500m, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
            var closed = new ContributionCycle(stokvel, "2026-09", 500m);
            var paidOut = new ContributionCycle(stokvel, "2026-08", 500m);
            closed.MarkClosedForSeed(new DateTime(2026, 10, 1, 0, 5, 0, DateTimeKind.Utc));
            paidOut.MarkPayoutProcessed();
            db.ContributionCycles.AddRange(open, closed, paidOut);
            db.Contributions.Add(new Contribution(stokvel, me, open.Id, 120.50m));
            await db.SaveChangesAsync();

            openId = open.Id; closedId = closed.Id; paidOutId = paidOut.Id;
        }

        var res = await ClientFor(me).GetAsync($"/api/v1/stokvels/{stokvel}/cycles");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cycles = (await res.Content.ReadFromJsonAsync<List<CycleSummaryResponse>>(JsonOptions.CaseInsensitive))!;

        Assert.Equal(new[] { "2026-10", "2026-09", "2026-08" }, cycles.Select(c => c.Label));
        Assert.Equal(new[] { openId, closedId, paidOutId }, cycles.Select(c => c.Id));
        Assert.Equal(new[] { "Open", "Closed", "PaidOut" }, cycles.Select(c => c.Status));

        Assert.Equal(new DateOnly(2026, 10, 31), cycles[0].DueDate);
        Assert.Null(cycles[1].DueDate);

        Assert.Equal(12050, cycles[0].CollectedCents);
        Assert.Equal(50000, cycles[0].AmountDueCents);
        Assert.Equal(0, cycles[1].CollectedCents);
    }

    [Fact]
    public async Task Cycles_NonMember_GetsNotFound()
    {
        var member = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Private Stokvel");
        await AddMemberAsync(stokvel, member);
        await CreateCycleAsync(stokvel, "2026-10");

        var res = await ClientFor(outsider).GetAsync($"/api/v1/stokvels/{stokvel}/cycles");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
