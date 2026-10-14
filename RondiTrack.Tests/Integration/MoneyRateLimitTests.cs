using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class MoneyRateLimitTests : IDisposable
{
    private const int Limit = 3;

    // Long enough that no token can return while a test is running.
    private const int NoRefillSeconds = 600;

    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;
    private readonly List<WebApplicationFactory<Program>> _hosts = new();

    public MoneyRateLimitTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();   // platform admin, used only to arrange data
    }

    public void Dispose()
    {
        foreach (var host in _hosts) host.Dispose();
    }

    // ---- helpers -------------------------------------------------------------------------

    // WithWebHostBuilder builds a new host, so each test gets its own fresh limiter.
    // Each test chooses its own replenish period, so no test depends on the clock.
    private WebApplicationFactory<Program> NewHost(int replenishSeconds)
    {
        var host = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimit:RecordContribution:TokenLimit", Limit.ToString());
            b.UseSetting("RateLimit:RecordContribution:TokensPerPeriod", Limit.ToString());
            b.UseSetting("RateLimit:RecordContribution:ReplenishSeconds", replenishSeconds.ToString());
        });
        _hosts.Add(host);
        return host;
    }

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>())!.Id;
    }

    private async Task<(Guid StokvelId, Guid CycleId, Guid[] Members)> ArrangeAsync(int memberCount)
    {
        var stokvelRes = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "Limit Stokvel", contributionAmount = 500 });
        stokvelRes.EnsureSuccessStatusCode();
        var stokvelId = (await stokvelRes.Content.ReadFromJsonAsync<StokvelResponse>())!.Id;

        var members = new Guid[memberCount];
        for (var i = 0; i < memberCount; i++)
        {
            members[i] = await CreateUserAsync();
            var add = await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId = members[i] });
            add.EnsureSuccessStatusCode();
        }

        var cycleRes = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label = "2026-10", targetAmount = 500 });
        cycleRes.EnsureSuccessStatusCode();
        var cycleId = (await cycleRes.Content.ReadFromJsonAsync<ContributionCycleResponse>())!.Id;

        return (stokvelId, cycleId, members);
    }

    private HttpClient ClientFor(WebApplicationFactory<Program> host, Guid userId)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.MintToken(userId));
        return client;
    }

    // A null key sends no Idempotency-Key header: the endpoint answers 400, but the limiter has
    // already spent a token, so this is a cheap way to use up the bucket without creating anything.
    private static HttpRequestMessage Contribution(Guid stokvelId, Guid cycleId, Guid userId, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId, contributionCycleId = cycleId, amount = 500 })
        };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static string? Header(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    private static async Task ExhaustAsync(HttpClient client, Guid stokvelId, Guid cycleId, Guid userId)
    {
        for (var i = 0; i < Limit; i++)
        {
            var res = await client.SendAsync(Contribution(stokvelId, cycleId, userId, null));
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
    }

    // ---- tests ---------------------------------------------------------------------------

    [Fact]
    public async Task A429_DoesNotConsumeTheKey_RetryAfterTheWindowCreatesOnce_AndNextRetryIsAReplay()
    {
        // 8 seconds: a slow machine cannot refill the bucket between exhausting it and the 429 check.
        var host = NewHost(replenishSeconds: 8);

        var (stokvelId, cycleId, members) = await ArrangeAsync(1);
        var userId = members[0];
        var client = ClientFor(host, userId);

        await ExhaustAsync(client, stokvelId, cycleId, userId);

        // Over the limit: 429 with Retry-After.
        var blocked = await client.SendAsync(Contribution(stokvelId, cycleId, userId, "K"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);

        // Wait as long as the server said, plus a margin, then the same key K is accepted.
        var wait = blocked.Headers.RetryAfter!.Delta!.Value + TimeSpan.FromSeconds(1);
        await Task.Delay(wait);
        var created = await client.SendAsync(Contribution(stokvelId, cycleId, userId, "K"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Null(Header(created, "Idempotency-Replayed"));

        // A third send of K is a replay, not a second contribution.
        var replay = await client.SendAsync(Contribution(stokvelId, cycleId, userId, "K"));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", Header(replay, "Idempotency-Replayed"));

        // Exactly one row in the database.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var rows = await db.Contributions.CountAsync(c => c.ContributionCycleId == cycleId);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task UserAExhaustingTheLimit_DoesNotAffectUserB()
    {
        var host = NewHost(NoRefillSeconds);

        var (stokvelId, cycleId, members) = await ArrangeAsync(2);
        var userA = members[0];
        var userB = members[1];

        var clientA = ClientFor(host, userA);
        await ExhaustAsync(clientA, stokvelId, cycleId, userA);
        var blocked = await clientA.SendAsync(Contribution(stokvelId, cycleId, userA, "A-key"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        var resB = await ClientFor(host, userB).SendAsync(Contribution(stokvelId, cycleId, userB, "B-key"));
        Assert.Equal(HttpStatusCode.Created, resB.StatusCode);
    }

    [Fact]
    public async Task TwoUsersPickingTheSameKey_DoNotCollide()
    {
        var host = NewHost(NoRefillSeconds);

        var (stokvelId, cycleId, members) = await ArrangeAsync(2);

        var first = await ClientFor(host, members[0]).SendAsync(Contribution(stokvelId, cycleId, members[0], "shared-key"));
        var second = await ClientFor(host, members[1]).SendAsync(Contribution(stokvelId, cycleId, members[1], "shared-key"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Null(Header(second, "Idempotency-Replayed"));
    }
}