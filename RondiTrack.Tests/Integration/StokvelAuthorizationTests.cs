using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Tests.Integration;

// Each test builds its own users, stokvel and cycle through the API (as the platform admin test user)
// and then acts as a minted-token user. Minting means a bug in the login code cannot fail these tests.
[Collection("Postgres collection")]
public class StokvelAuthorizationTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public StokvelAuthorizationTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient(); // the platform admin test user by default
    }

    // ---- helpers -------------------------------------------------------------------------

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>())!.Id;
    }

    private async Task<Guid> CreateStokvelAsync()
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "Test Stokvel", contributionAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<StokvelResponse>())!.Id;
    }

    private async Task AddMemberAsync(Guid stokvelId, Guid userId, StokvelMemberRole role = StokvelMemberRole.Member)
    {
        var res = await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId });
        res.EnsureSuccessStatusCode();

        if (role == StokvelMemberRole.Member) return;

        // The API always adds members as Member, so set the higher role directly in the database.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var row = await db.StokvelMembers.SingleAsync(m => m.StokvelId == stokvelId && m.UserId == userId);
        db.Entry(row).Property(m => m.Role).CurrentValue = role;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateCycleAsync(Guid stokvelId)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label = "2026-10", targetAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ContributionCycleResponse>())!.Id;
    }

    private static async Task<HttpResponseMessage> RecordContributionAsync(
        HttpClient client, Guid stokvelId, Guid cycleId, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId, contributionCycleId = cycleId, amount = 500 })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private sealed record Scenario(
        Guid StokvelId, Guid CycleId, Guid NonMemberId, Guid MemberId, Guid OtherMemberId, Guid TreasurerId);

    private async Task<Scenario> ArrangeAsync()
    {
        var stokvelId = await CreateStokvelAsync();
        var nonMember = await CreateUserAsync();
        var member = await CreateUserAsync();
        var otherMember = await CreateUserAsync();
        var treasurer = await CreateUserAsync();

        await AddMemberAsync(stokvelId, member);
        await AddMemberAsync(stokvelId, otherMember);
        await AddMemberAsync(stokvelId, treasurer, StokvelMemberRole.Treasurer);

        var cycleId = await CreateCycleAsync(stokvelId);
        return new Scenario(stokvelId, cycleId, nonMember, member, otherMember, treasurer);
    }

    private static string PayoutUrl(Scenario s) =>
        $"/api/v1/stokvels/{s.StokvelId}/cycles/{s.CycleId}/payout";

    // ---- 401 -----------------------------------------------------------------------------

    [Fact]
    public async Task NoToken_Returns401ProblemJson()
    {
        var client = _factory.CreateAnonymousClient();

        var res = await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}/members");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    // ---- 404 / 403 / success on the same endpoint (process payout) -------------------------

    [Fact]
    public async Task ProcessPayout_AsNonMember_Returns404()
    {
        var s = await ArrangeAsync();
        var client = _factory.CreateClientFor(s.NonMemberId);

        var res = await client.PostAsync(PayoutUrl(s), null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProcessPayout_AsMemberWithoutRole_Returns403()
    {
        var s = await ArrangeAsync();
        var client = _factory.CreateClientFor(s.MemberId);

        var res = await client.PostAsync(PayoutUrl(s), null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProcessPayout_AsTreasurer_Returns201()
    {
        var s = await ArrangeAsync();
        var client = _factory.CreateClientFor(s.TreasurerId);

        var res = await client.PostAsync(PayoutUrl(s), null);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    // ---- own contribution vs someone else's ----------------------------------------------

    [Fact]
    public async Task RecordContribution_ForSelf_AsMember_Returns201()
    {
        var s = await ArrangeAsync();
        var client = _factory.CreateClientFor(s.MemberId);

        var res = await RecordContributionAsync(client, s.StokvelId, s.CycleId, s.MemberId);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task RecordContribution_ForSomeoneElse_AsMember_Returns403()
    {
        var s = await ArrangeAsync();
        var client = _factory.CreateClientFor(s.MemberId);

        var res = await RecordContributionAsync(client, s.StokvelId, s.CycleId, s.OtherMemberId);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RecordContribution_ForSomeoneElse_AsTreasurer_Returns201()
    {
        var s = await ArrangeAsync();
        var client = _factory.CreateClientFor(s.TreasurerId);

        var res = await RecordContributionAsync(client, s.StokvelId, s.CycleId, s.MemberId);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    // ---- the one plain role check ----------------------------------------------------------

    [Fact]
    public async Task ListAllStokvels_AsOrdinaryUser_Returns403_AndAsPlatformAdmin_Returns200()
    {
        var userId = await CreateUserAsync();

        var ordinary = await _factory.CreateClientFor(userId).GetAsync("/api/v1/stokvels");
        var admin = await _admin.GetAsync("/api/v1/stokvels");

        Assert.Equal(HttpStatusCode.Forbidden, ordinary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }
}