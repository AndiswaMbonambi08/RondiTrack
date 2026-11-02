using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Tests.Integration;

// Assignment 8.3: the contribution history (filters, half-open UTC range, token bound to its filter)
// and the activity feed (only my stokvels, names only for Treasurers and Admins).
[Collection("Postgres collection")]
public class HistoryAndActivityTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public HistoryAndActivityTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();   // platform admin, used only to arrange data
    }

    private sealed record Page<T>(List<T> Items, string NextPageToken);

    // ---- helpers -------------------------------------------------------------------------

    private async Task<Guid> CreateUserAsync(string fullName)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/users",
            new { fullName, email = $"{Guid.NewGuid():N}@test.example" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task<Guid> CreateStokvelAsync()
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "History Stokvel", contributionAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task AddMemberAsync(Guid stokvelId, Guid userId, StokvelMemberRole role = StokvelMemberRole.Member)
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

    private async Task<Guid> CreateCycleAsync(Guid stokvelId, string label = "2026-10")
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label, targetAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    // Written straight to the database so a test can pick the exact instant.
    private async Task<Guid> SeedContributionAsync(Guid stokvelId, Guid userId, Guid cycleId, DateTime atUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var contribution = new Contribution(stokvelId, userId, cycleId, 500m, atUtc, ContributionStatus.Paid);
        db.Contributions.Add(contribution);
        await db.SaveChangesAsync();
        return contribution.Id;
    }

    private static DateTime Utc(string iso) =>
        DateTime.SpecifyKind(DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);

    private static string History(Guid stokvelId, string query = "") =>
        $"/api/v1/stokvels/{stokvelId}/contribution-history{query}";

    private static async Task<Page<T>> GetPageAsync<T>(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"{url}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<Page<T>>(JsonOptions.CaseInsensitive))!;
    }

    private sealed record Arranged(Guid StokvelId, Guid CycleId, Guid TreasurerId, HttpClient Treasurer);

    private async Task<Arranged> ArrangeTreasurerAsync()
    {
        var stokvelId = await CreateStokvelAsync();
        var treasurerId = await CreateUserAsync("Hist Treasurer");
        await AddMemberAsync(stokvelId, treasurerId, StokvelMemberRole.Treasurer);
        var cycleId = await CreateCycleAsync(stokvelId);
        return new Arranged(stokvelId, cycleId, treasurerId, _factory.CreateClientFor(treasurerId));
    }

    // ---- the half-open range [from, to) ---------------------------------------------------

    [Fact]
    public async Task TimeRange_IncludesFrom_ExcludesTo()
    {
        var a = await ArrangeTreasurerAsync();
        var instants = new[]
        {
            "2026-09-30T21:59:59Z",   // just before from: out
            "2026-09-30T22:00:00Z",   // exactly from: in
            "2026-10-31T21:59:59Z",   // just before to: in
            "2026-10-31T22:00:00Z",   // exactly to: out
        };
        var ids = new List<Guid>();
        foreach (var iso in instants)
        {
            var user = await CreateUserAsync("Range Member");
            await AddMemberAsync(a.StokvelId, user);
            ids.Add(await SeedContributionAsync(a.StokvelId, user, a.CycleId, Utc(iso)));
        }

        var page = await GetPageAsync<HistoryItemResponse>(a.Treasurer,
            History(a.StokvelId, "?from=2026-09-30T22:00:00Z&to=2026-10-31T22:00:00Z"));

        Assert.Equal(new[] { ids[2], ids[1] }, page.Items.Select(i => i.Id).ToArray());   // newest first
    }

    // ---- filters, sort, paging --------------------------------------------------------------

    [Fact]
    public async Task Treasurer_CanFilterToOneMember_AndSortOldestFirst()
    {
        var a = await ArrangeTreasurerAsync();
        var sipho = await CreateUserAsync("Sipho");
        var thandi = await CreateUserAsync("Thandi");
        await AddMemberAsync(a.StokvelId, sipho);
        await AddMemberAsync(a.StokvelId, thandi);
        var cycle2 = await CreateCycleAsync(a.StokvelId, "2026-11");
        var s1 = await SeedContributionAsync(a.StokvelId, sipho, a.CycleId, Utc("2026-10-02T08:00:00Z"));
        var s2 = await SeedContributionAsync(a.StokvelId, sipho, cycle2, Utc("2026-10-03T08:00:00Z"));
        await SeedContributionAsync(a.StokvelId, thandi, a.CycleId, Utc("2026-10-04T08:00:00Z"));

        var newest = await GetPageAsync<HistoryItemResponse>(a.Treasurer, History(a.StokvelId, $"?member={sipho}"));
        var oldest = await GetPageAsync<HistoryItemResponse>(a.Treasurer, History(a.StokvelId, $"?member={sipho}&sort=oldest"));

        Assert.Equal(new[] { s2, s1 }, newest.Items.Select(i => i.Id).ToArray());
        Assert.Equal(new[] { s1, s2 }, oldest.Items.Select(i => i.Id).ToArray());
        Assert.All(newest.Items, i => Assert.Equal("Sipho", i.MemberName));
    }

    [Fact]
    public async Task Paging_WalksEveryRowOnce()
    {
        var a = await ArrangeTreasurerAsync();
        for (var i = 0; i < 5; i++)
        {
            var user = await CreateUserAsync($"Walker {i}");
            await AddMemberAsync(a.StokvelId, user);
            await SeedContributionAsync(a.StokvelId, user, a.CycleId, Utc("2026-10-10T08:00:00Z").AddMinutes(i));
        }

        var all = await GetPageAsync<HistoryItemResponse>(a.Treasurer, History(a.StokvelId, "?pageSize=100"));
        var walked = new List<Guid>();
        string? token = null;
        for (var guard = 0; guard < 20; guard++)
        {
            var url = History(a.StokvelId, "?pageSize=2" + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token)));
            var page = await GetPageAsync<HistoryItemResponse>(a.Treasurer, url);
            walked.AddRange(page.Items.Select(i => i.Id));
            token = page.NextPageToken;
            if (string.IsNullOrEmpty(token)) break;
        }

        Assert.Equal(all.Items.Select(i => i.Id).ToList(), walked);
        Assert.Equal(walked.Count, walked.Distinct().Count());
    }

    // ---- question 4: a token only works with the filters it came from -------------------------

    [Fact]
    public async Task TokenFromOneFilter_WithAnotherFilter_Returns400()
    {
        var a = await ArrangeTreasurerAsync();
        for (var i = 0; i < 2; i++)
        {
            var user = await CreateUserAsync($"Token {i}");
            await AddMemberAsync(a.StokvelId, user);
            await SeedContributionAsync(a.StokvelId, user, a.CycleId, Utc("2026-10-10T08:00:00Z").AddMinutes(i));
        }

        var first = await GetPageAsync<HistoryItemResponse>(a.Treasurer, History(a.StokvelId, "?pageSize=1"));
        Assert.False(string.IsNullOrEmpty(first.NextPageToken));

        var res = await a.Treasurer.GetAsync(History(a.StokvelId,
            "?pageSize=1&to=2030-01-01T00:00:00Z&pageToken=" + Uri.EscapeDataString(first.NextPageToken)));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains("pageToken", await res.Content.ReadAsStringAsync());
    }

    // ---- question 5: bad input and who may ask --------------------------------------------------

    [Theory]
    [InlineData("?from=banana")]
    [InlineData("?from=2026-10-01")]                        // a date with no time: refused, never read as midnight UTC
    [InlineData("?to=yesterday")]
    [InlineData("?member=banana")]
    [InlineData("?sort=sideways")]
    [InlineData("?from=2026-10-31T22:00:00Z&to=2026-09-30T22:00:00Z")]   // to before from
    public async Task BadInput_Returns400_AsProblemJson(string query)
    {
        var a = await ArrangeTreasurerAsync();

        var res = await a.Treasurer.GetAsync(History(a.StokvelId, query));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Treasurer_AskingForSomeoneWhoIsNotInThisStokvel_GetsAnEmptyList()
    {
        var a = await ArrangeTreasurerAsync();
        var stranger = await CreateUserAsync("Stranger");

        var page = await GetPageAsync<HistoryItemResponse>(a.Treasurer, History(a.StokvelId, $"?member={stranger}"));

        Assert.Empty(page.Items);
        Assert.Equal("", page.NextPageToken);
    }

    [Fact]
    public async Task PlainMember_SeesOnlyOwnContributions_AndAnotherMemberIs403()
    {
        var a = await ArrangeTreasurerAsync();
        var me = await CreateUserAsync("Plain Me");
        var other = await CreateUserAsync("Plain Other");
        await AddMemberAsync(a.StokvelId, me);
        await AddMemberAsync(a.StokvelId, other);
        var mine = await SeedContributionAsync(a.StokvelId, me, a.CycleId, Utc("2026-10-05T08:00:00Z"));
        await SeedContributionAsync(a.StokvelId, other, a.CycleId, Utc("2026-10-06T08:00:00Z"));
        var client = _factory.CreateClientFor(me);

        var page = await GetPageAsync<HistoryItemResponse>(client, History(a.StokvelId));
        var forbidden = await client.GetAsync(History(a.StokvelId, $"?member={other}"));
        var self = await GetPageAsync<HistoryItemResponse>(client, History(a.StokvelId, $"?member={me}"));

        Assert.Equal(new[] { mine }, page.Items.Select(i => i.Id).ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(new[] { mine }, self.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task NonMember_Gets404_AndNoToken_Gets401()
    {
        var a = await ArrangeTreasurerAsync();
        var outsider = await CreateUserAsync("Outsider");

        var notFound = await _factory.CreateClientFor(outsider).GetAsync(History(a.StokvelId));
        var unauthorized = await _factory.CreateAnonymousClient().GetAsync(History(a.StokvelId));

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    // ---- the activity feed --------------------------------------------------------------------------

    [Fact]
    public async Task Activity_ShowsOnlyMyStokvels_AndNamesOnlyToTreasurers()
    {
        var me = await CreateUserAsync("Feed Me");
        var cee = await CreateUserAsync("Cee");
        var bee = await CreateUserAsync("Bee");
        var dee = await CreateUserAsync("Dee");

        var s1 = await CreateStokvelAsync();   // I am a plain Member here
        var s2 = await CreateStokvelAsync();   // I am the Treasurer here
        var s3 = await CreateStokvelAsync();   // I do not belong here
        await AddMemberAsync(s1, me);
        await AddMemberAsync(s1, cee);
        await AddMemberAsync(s2, me, StokvelMemberRole.Treasurer);
        await AddMemberAsync(s2, bee);
        await AddMemberAsync(s3, dee);
        var c1 = await CreateCycleAsync(s1);
        var c2 = await CreateCycleAsync(s2);
        var c3 = await CreateCycleAsync(s3);

        var mine = await SeedContributionAsync(s1, me, c1, Utc("2026-10-01T08:00:00Z"));
        var ceeIn1 = await SeedContributionAsync(s1, cee, c1, Utc("2026-10-02T08:00:00Z"));
        var beeIn2 = await SeedContributionAsync(s2, bee, c2, Utc("2026-10-03T08:00:00Z"));
        var deeIn3 = await SeedContributionAsync(s3, dee, c3, Utc("2026-10-04T08:00:00Z"));
        var client = _factory.CreateClientFor(me);

        var feed = await GetPageAsync<ActivityItemResponse>(client, "/api/v1/me/activity?pageSize=100");
        var ids = feed.Items.Select(i => i.Id).ToHashSet();

        Assert.Contains(mine, ids);
        Assert.Contains(beeIn2, ids);              // I am Treasurer of s2: I see Bee's contribution
        Assert.DoesNotContain(ceeIn1, ids);        // plain Member of s1: not Cee's
        Assert.DoesNotContain(deeIn3, ids);        // not my stokvel
        Assert.DoesNotContain(feed.Items, i => i.StokvelId == s3);
        Assert.Equal("Bee", feed.Items.Single(i => i.Id == beeIn2).ActorName);
        Assert.DoesNotContain(feed.Items, i => i.StokvelId == s1 && i.ActorName == "Cee");
        Assert.Equal(feed.Items.OrderByDescending(i => i.OccurredAt).Select(i => i.OccurredAt),
                     feed.Items.Select(i => i.OccurredAt));
    }

    [Fact]
    public async Task Activity_Paging_WalksEveryItemOnce_AndShowsPayouts()
    {
        var me = await CreateUserAsync("Walker Me");
        var other = await CreateUserAsync("Walker Other");
        var stokvelId = await CreateStokvelAsync();
        await AddMemberAsync(stokvelId, me);
        await AddMemberAsync(stokvelId, other);
        var cycleId = await CreateCycleAsync(stokvelId);
        await SeedContributionAsync(stokvelId, me, cycleId, Utc("2026-10-01T08:00:00Z"));
        var payout = await _admin.PostAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout", null);
        Assert.True(payout.IsSuccessStatusCode, "Payout: " + (int)payout.StatusCode);
        var client = _factory.CreateClientFor(me);

        var all = await GetPageAsync<ActivityItemResponse>(client, "/api/v1/me/activity?pageSize=100");
        var walked = new List<(string Kind, Guid Id)>();
        string? token = null;
        for (var guard = 0; guard < 50; guard++)
        {
            var url = "/api/v1/me/activity?pageSize=1" + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token));
            var page = await GetPageAsync<ActivityItemResponse>(client, url);
            walked.AddRange(page.Items.Select(i => (i.Kind, i.Id)));
            token = page.NextPageToken;
            if (string.IsNullOrEmpty(token)) break;
        }

        Assert.Equal(all.Items.Select(i => (i.Kind, i.Id)).ToList(), walked);
        Assert.Contains(all.Items, i => i.Kind == "Payout" && i.StokvelId == stokvelId && i.AmountCents == 50000);
        Assert.Contains(all.Items, i => i.Kind == "MemberJoined" && i.StokvelId == stokvelId);
    }

    [Fact]
    public async Task Activity_TokenFromSomeoneElse_Returns400_AndNoToken_Returns401()
    {
        var me = await CreateUserAsync("Token Me");
        var you = await CreateUserAsync("Token You");
        var stokvelId = await CreateStokvelAsync();
        await AddMemberAsync(stokvelId, me);
        await AddMemberAsync(stokvelId, you);
        var first = await GetPageAsync<ActivityItemResponse>(_factory.CreateClientFor(me), "/api/v1/me/activity?pageSize=1");
        Assert.False(string.IsNullOrEmpty(first.NextPageToken));

        var res = await _factory.CreateClientFor(you).GetAsync(
            "/api/v1/me/activity?pageSize=1&pageToken=" + Uri.EscapeDataString(first.NextPageToken));
        var anonymous = await _factory.CreateAnonymousClient().GetAsync("/api/v1/me/activity");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}