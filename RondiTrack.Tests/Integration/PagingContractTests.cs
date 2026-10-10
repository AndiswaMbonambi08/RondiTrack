using RondiTrack.Tests.TestSupport;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class PagingContractTests
{
    private readonly HttpClient _client;
    public PagingContractTests(PostgresApiFactory factory) => _client = factory.CreateClient();

    private record Page(List<ContributionResponse> Items, string NextPageToken);

    private async Task<(Guid stokvelId, Guid cycleId)> SetupWithContributionsAsync(int count)
    {
        var stokvel = (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Paging Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;
        var cycle = (await (await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId = stokvel.Id, label = "2026-09", targetAmount = 100m }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

        for (var i = 0; i < count; i++)
        {
            var user = (await (await _client.PostAsJsonAsync("/api/users", new { fullName = $"P{i}", email = $"{Guid.NewGuid()}@example.com" }))
                .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;
            (await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id })).EnsureSuccessStatusCode();

            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions")
            { Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amount = 100m }) };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            (await _client.SendAsync(req)).EnsureSuccessStatusCode();
        }
        return (stokvel.Id, cycle.Id);
    }

    private static string Url(Guid s, Guid c, string query = "") =>
        $"/api/stokvels/{s}/cycles/{c}/contributions{query}";

    [Fact]
    public async Task Paging_WalksEveryRowOnceAndEndsWithEmptyToken()
    {
        var (s, c) = await SetupWithContributionsAsync(5);
        var seen = new List<Guid>();
        var token = "";
        do
        {
            var page = (await _client.GetFromJsonAsync<Page>(Url(s, c, $"?pageSize=2&pageToken={token}"), JsonOptions.CaseInsensitive))!;
            seen.AddRange(page.Items.Select(i => i.Id));
            token = page.NextPageToken;
        } while (token != "");

        Assert.Equal(5, seen.Count);
        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public async Task NegativePageSize_Returns400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(Url(Guid.NewGuid(), Guid.NewGuid(), "?pageSize=-1"))).StatusCode);

    [Fact]
    public async Task OversizedPageSize_IsReducedNotRejected()
    {
        var (s, c) = await SetupWithContributionsAsync(1);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(Url(s, c, "?pageSize=100000"))).StatusCode);
    }

    [Fact]
    public async Task UnknownSortField_Returns400()
    {
        var (s, c) = await SetupWithContributionsAsync(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(Url(s, c, "?orderBy=idempotencyKey"))).StatusCode);
    }

    [Fact]
    public async Task TokenReusedWithDifferentSort_Returns400()
    {
        var (s, c) = await SetupWithContributionsAsync(3);
        var page = (await _client.GetFromJsonAsync<Page>(Url(s, c, "?pageSize=1"), JsonOptions.CaseInsensitive))!;
        Assert.NotEqual("", page.NextPageToken);

        var res = await _client.GetAsync(Url(s, c, $"?pageSize=1&orderBy=amount&pageToken={page.NextPageToken}"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
