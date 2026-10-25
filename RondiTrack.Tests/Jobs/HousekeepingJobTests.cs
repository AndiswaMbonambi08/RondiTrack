using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Jobs;

[Collection("Jobs collection")]
public class HousekeepingJobTests : JobTestBase
{
    public HousekeepingJobTests(JobsApiFactory factory) : base(factory) { }

    [Fact]
    public async Task TokenInsideRetentionSurvives_TokenPastItIsDeleted()
    {
        var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var nowUtc = now.UtcDateTime;
        var user = new User("Token Owner", $"{Guid.NewGuid():N}@test.example");
        var family = Guid.NewGuid();

        // Retention is 30 days AFTER expiry. Lifetime is 7 days, so expiry = created + 7 days.
        var expired29DaysAgo = new RefreshToken(user.Id, family, Guid.NewGuid().ToString("N"), nowUtc.AddDays(-36), TimeSpan.FromDays(7));
        var expired31DaysAgo = new RefreshToken(user.Id, family, Guid.NewGuid().ToString("N"), nowUtc.AddDays(-38), TimeSpan.FromDays(7));
        var stillValid = new RefreshToken(user.Id, family, Guid.NewGuid().ToString("N"), nowUtc.AddDays(-1), TimeSpan.FromDays(7));

        await WithDbAsync(async db =>
        {
            db.Users.Add(user);
            db.RefreshTokens.AddRange(expired29DaysAgo, expired31DaysAgo, stillValid);
            await db.SaveChangesAsync();
            return 0;
        });

        var result = await NewHousekeepingJob(new FakeClock(now), tokenDays: 30).RunOnceAsync();

        Assert.Equal(1, result.RefreshTokensDeleted);
        await WithDbAsync(async db =>
        {
            var ids = await db.RefreshTokens.AsNoTracking().Select(t => t.Id).ToListAsync();
            Assert.Contains(expired29DaysAgo.Id, ids);
            Assert.Contains(stillValid.Id, ids);
            Assert.DoesNotContain(expired31DaysAgo.Id, ids);
            return 0;
        });
    }

    [Fact]
    public async Task IdempotencyRecordInsideRetention_StillReplays()
    {
        var (stokvelId, userId, cycleId) = await SetUpContributionAsync();
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await PostContributionAsync(stokvelId, userId, cycleId, key)).StatusCode);

        // Two days later is inside the 7-day (168 hour) retention.
        await NewHousekeepingJob(new FakeClock(DateTimeOffset.UtcNow.AddDays(2))).RunOnceAsync();

        var retry = await PostContributionAsync(stokvelId, userId, cycleId, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.True(retry.Headers.TryGetValues("Idempotency-Replayed", out var values) && values.Single() == "true");
    }

    [Fact]
    public async Task IdempotencyRecordPastRetention_IsDeleted_AndALateRetryIsRejectedWith409()
    {
        var (stokvelId, userId, cycleId) = await SetUpContributionAsync();
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await PostContributionAsync(stokvelId, userId, cycleId, key)).StatusCode);

        var result = await NewHousekeepingJob(new FakeClock(DateTimeOffset.UtcNow.AddDays(10))).RunOnceAsync();
        Assert.Equal(1, result.IdempotencyEntriesDeleted);

        // The record is gone, so the retry is treated as a new request. The one-contribution-per-member-
        // per-cycle rule stops it: money is never recorded twice, the client just sees 409 not a replay.
        var late = await PostContributionAsync(stokvelId, userId, cycleId, key);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        await WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Contributions.CountAsync(c => c.ContributionCycleId == cycleId));
            return 0;
        });
    }

    // ---- HTTP helpers (the default client is the platform admin test user) ----

    private static async Task<Guid> IdOf(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<(Guid StokvelId, Guid UserId, Guid CycleId)> SetUpContributionAsync()
    {
        var client = Factory.CreateClient();
        var userId = await IdOf(await client.PostAsJsonAsync("/api/v1/users",
            new { fullName = "Test User", email = $"{Guid.NewGuid():N}@test.example" }));
        var stokvelId = await IdOf(await client.PostAsJsonAsync("/api/v1/stokvels",
            new { name = "Housekeeping Test", contributionAmount = 500 }));
        (await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId })).EnsureSuccessStatusCode();
        var cycleId = await IdOf(await client.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label = "2026-11", targetAmount = 500 }));
        return (stokvelId, userId, cycleId);
    }

    private Task<HttpResponseMessage> PostContributionAsync(Guid stokvelId, Guid userId, Guid cycleId, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId, contributionCycleId = cycleId, amountInCents = 50000 })
        };
        req.Headers.Add("Idempotency-Key", key);
        return Factory.CreateClient().SendAsync(req);
    }
}
