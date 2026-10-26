using RondiTrack.Tests.TestSupport;
// Proves idempotency through the real pipeline: repeating a request with the same key
// returns the identical response, and reusing a key with a different payload is rejected.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class IdempotencyIntegrationTests
{
    private readonly HttpClient _client;

    public IdempotencyIntegrationTests(PostgresApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(UserResponse User, StokvelResponse Stokvel, ContributionCycleResponse Cycle)> SetupAsync()
    {
        var user = (await (await _client.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email = $"{Guid.NewGuid()}@example.com" }))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;
        var stokvel = (await (await _client.PostAsJsonAsync("/api/v1/stokvels", new { name = "Test Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;
        await _client.PostAsJsonAsync($"/api/v1/stokvels/{stokvel.Id}/members", new { userId = user.Id });
        var cycle = (await (await _client.PostAsJsonAsync("/api/v1/contribution-cycles", new { stokvelId = stokvel.Id, label = "2026-09", targetAmount = 100m }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

        return (user, stokvel, cycle);
    }

    private static string? Header(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    [Fact]
    public async Task SameIdempotencyKeySameBody_ReturnsIdenticalResponseBothTimes()
    {
        var (user, stokvel, cycle) = await SetupAsync();
        var body = new { userId = user.Id, contributionCycleId = cycle.Id, amountInCents = 10000 };

        var first = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvel.Id}/contributions") { Content = JsonContent.Create(body) };
        first.Headers.Add("Idempotency-Key", "same-key");
        var firstResponse = await _client.SendAsync(first);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ContributionResponse>(JsonOptions.CaseInsensitive);

        var second = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvel.Id}/contributions") { Content = JsonContent.Create(body) };
        second.Headers.Add("Idempotency-Key", "same-key");
        var secondResponse = await _client.SendAsync(second);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ContributionResponse>(JsonOptions.CaseInsensitive);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Null(Header(firstResponse, "Idempotency-Replayed"));
        Assert.Equal("true", Header(secondResponse, "Idempotency-Replayed"));
        Assert.Equal(firstBody!.Id, secondBody!.Id);
        Assert.Equal(firstBody.RecordedAt, secondBody.RecordedAt);
        Assert.Equal(10000, secondBody.AmountCents);
    }

    [Fact]
    public async Task SameIdempotencyKeyDifferentAmount_Returns422ProblemJson()
    {
        var (user, stokvel, cycle) = await SetupAsync();

        var first = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvel.Id}/contributions")
        {
            Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amountInCents = 10000 })
        };
        first.Headers.Add("Idempotency-Key", "reused-key");
        await _client.SendAsync(first);

        var second = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvel.Id}/contributions")
        {
            Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amountInCents = 20000 })
        };
        second.Headers.Add("Idempotency-Key", "reused-key");
        var response = await _client.SendAsync(second);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }
}
