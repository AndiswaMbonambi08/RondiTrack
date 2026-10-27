using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class StokvelDetailTests : IDisposable
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;
    private readonly WebApplicationFactory<Program> _app;

    public StokvelDetailTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
        _app = factory.WithWebHostBuilder(_ => { });
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

    // Status, title and detail: everything except the per-request trace ids.
    private static async Task<(HttpStatusCode Status, string? Title, string? Detail)> ProblemOf(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return (res.StatusCode,
            root.GetProperty("title").GetString(),
            root.TryGetProperty("detail", out var d) ? d.GetString() : null);
    }

    [Fact]
    public async Task Member_Gets200_WithNameAmountRoleCountAndCycleSummary()
    {
        var me = await CreateUserAsync();
        var other = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Detail Stokvel");
        await AddMemberAsync(stokvel, me, StokvelMemberRole.Member);
        await AddMemberAsync(stokvel, other, StokvelMemberRole.Member);
        var cycle = await CreateCycleAsync(stokvel, "2026-10");

        var client = ClientFor(me);
        var pay = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvel}/contributions")
        {
            Content = JsonContent.Create(new { userId = me, contributionCycleId = cycle, amountInCents = 50000 })
        };
        pay.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(pay)).StatusCode);

        var res = await client.GetAsync($"/api/v1/stokvels/{stokvel}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<StokvelDetailResponse>())!;
        Assert.Equal(stokvel, body.Id);
        Assert.Equal("Detail Stokvel", body.Name);
        Assert.Equal(50000, body.ContributionCents);
        Assert.Equal("Member", body.Role);
        Assert.Equal(2, body.MemberCount);
        Assert.Equal(cycle, body.CurrentCycle!.Id);
        Assert.Equal("2026-10", body.CurrentCycle.Label);
        Assert.Equal("Open", body.CurrentCycle.Status);
        Assert.Equal(1, body.CurrentCycle.ContributorCount);
        Assert.Equal(50000, body.CurrentCycle.CollectedCents);
    }

    [Fact]
    public async Task Role_IsPerStokvel_NotPerUser()
    {
        var me = await CreateUserAsync();
        var family = await CreateStokvelAsync("Family");
        var work = await CreateStokvelAsync("Work");
        await AddMemberAsync(family, me, StokvelMemberRole.Treasurer);
        await AddMemberAsync(work, me, StokvelMemberRole.Member);

        var client = ClientFor(me);
        var inFamily = (await (await client.GetAsync($"/api/v1/stokvels/{family}"))
            .Content.ReadFromJsonAsync<StokvelDetailResponse>())!;
        var inWork = (await (await client.GetAsync($"/api/v1/stokvels/{work}"))
            .Content.ReadFromJsonAsync<StokvelDetailResponse>())!;

        Assert.Equal("Treasurer", inFamily.Role);
        Assert.Equal("Member", inWork.Role);
    }

    [Fact]
    public async Task StokvelWithNoCycle_HasNullCurrentCycle()
    {
        var me = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("No Cycle Yet");
        await AddMemberAsync(stokvel, me, StokvelMemberRole.Member);

        var res = await ClientFor(me).GetAsync($"/api/v1/stokvels/{stokvel}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<StokvelDetailResponse>())!;
        Assert.Null(body.CurrentCycle);
    }

    [Fact]
    public async Task NonMember_UnknownId_AndMalformedId_AllGetTheSame404()
    {
        var member = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Secret Circle Name");
        await AddMemberAsync(stokvel, member, StokvelMemberRole.Member);

        var client = ClientFor(outsider);
        var nonMemberRes = await client.GetAsync($"/api/v1/stokvels/{stokvel}");
        var nonMemberText = await nonMemberRes.Content.ReadAsStringAsync();
        var nonMember = await ProblemOf(await client.GetAsync($"/api/v1/stokvels/{stokvel}"));
        var unknown = await ProblemOf(await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}"));
        var malformed = await ProblemOf(await client.GetAsync("/api/v1/stokvels/not-a-guid"));

        Assert.Equal(HttpStatusCode.NotFound, nonMember.Status);
        Assert.Equal(nonMember, unknown);
        Assert.Equal(nonMember, malformed);
        Assert.DoesNotContain("Secret Circle Name", nonMemberText);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        var res = await _factory.CreateAnonymousClient().GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Response_ContainsNoMemberIds()
    {
        var me = await CreateUserAsync();
        var other = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync("Privacy Detail");
        await AddMemberAsync(stokvel, me, StokvelMemberRole.Member);
        await AddMemberAsync(stokvel, other, StokvelMemberRole.Member);
        await CreateCycleAsync(stokvel, "2026-10");

        var body = await (await ClientFor(me).GetAsync($"/api/v1/stokvels/{stokvel}")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(other.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(me.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }
}
