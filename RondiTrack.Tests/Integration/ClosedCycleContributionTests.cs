using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

// Assignment 7.4: a closed or paid-out cycle takes no contributions, and the API says why.
// The form shows this detail when a cycle closes while it is open, so the API must be the one to refuse.
[Collection("Postgres collection")]
public class ClosedCycleContributionTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public ClosedCycleContributionTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();   // platform admin, used only to arrange data
    }

    private async Task<(Guid StokvelId, Guid CycleId, Guid MemberId, HttpClient Member)> ArrangeAsync()
    {
        var userRes = await _admin.PostAsJsonAsync("/api/v1/users",
            new { fullName = "Closed Tester", email = $"{Guid.NewGuid():N}@test.example" });
        userRes.EnsureSuccessStatusCode();
        var memberId = (await userRes.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;

        var stokvelRes = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "Closed Stokvel", contributionAmount = 500 });
        stokvelRes.EnsureSuccessStatusCode();
        var stokvelId = (await stokvelRes.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!.Id;

        (await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId = memberId })).EnsureSuccessStatusCode();

        var cycleRes = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label = "2026-10", targetAmount = 500 });
        cycleRes.EnsureSuccessStatusCode();
        var cycleId = (await cycleRes.Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!.Id;

        return (stokvelId, cycleId, memberId, _factory.CreateClientFor(memberId));
    }

    private async Task CloseAsync(Guid cycleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var cycle = await db.ContributionCycles.SingleAsync(c => c.Id == cycleId);
        cycle.MarkClosedForSeed(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task PayOutAsync(Guid cycleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var cycle = await db.ContributionCycles.SingleAsync(c => c.Id == cycleId);
        cycle.MarkPayoutProcessed();
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid stokvelId, Guid cycleId, Guid memberId, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId = memberId, contributionCycleId = cycleId, amountInCents = 15050 })
        };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task AssertConflictWithDetail(HttpResponseMessage res, string expectedText)
    {
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Contains(expectedText, problem.RootElement.GetProperty("detail").GetString());
        Assert.True(problem.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task ClosedCycle_Returns409_WithADetailAndACorrelationId()
    {
        var a = await ArrangeAsync();
        await CloseAsync(a.CycleId);

        var res = await PostAsync(a.Member, a.StokvelId, a.CycleId, a.MemberId, Guid.NewGuid().ToString());

        await AssertConflictWithDetail(res, "is closed");
    }

    [Fact]
    public async Task PaidOutCycle_Returns409_WithADetailAndACorrelationId()
    {
        var a = await ArrangeAsync();
        await PayOutAsync(a.CycleId);

        var res = await PostAsync(a.Member, a.StokvelId, a.CycleId, a.MemberId, Guid.NewGuid().ToString());

        await AssertConflictWithDetail(res, "paid out");
    }

    [Fact]
    public async Task RetryOfAPaymentThatLanded_StillReplays_AfterTheCycleCloses()
    {
        var a = await ArrangeAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(a.Member, a.StokvelId, a.CycleId, a.MemberId, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await CloseAsync(a.CycleId);

        var retry = await PostAsync(a.Member, a.StokvelId, a.CycleId, a.MemberId, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.True(retry.Headers.TryGetValues("Idempotency-Replayed", out var values) && values.Single() == "true");
    }
}
