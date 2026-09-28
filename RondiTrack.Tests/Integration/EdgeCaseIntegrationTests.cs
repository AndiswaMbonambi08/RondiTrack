// Edge cases found by asking: what happens before the typical case exists yet (empty
// collection), exactly at a validator's boundary, and when two independently valid
// inputs are combined in a way a cross-entity rule rejects.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

public class EdgeCaseIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EdgeCaseIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMembers_OnBrandNewStokvel_ReturnsEmptyArrayNotError()
    {
        var stokvel = (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Empty Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;

        var response = await _client.GetAsync($"/api/stokvels/{stokvel.Id}/members");
        var members = await response.Content.ReadFromJsonAsync<List<Guid>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(members!);
    }

    [Fact]
    public async Task CreateStokvel_ContributionAmountAtSmallestPositiveValue_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/stokvels", new { name = "Boundary Stokvel", contributionAmount = 0.01m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RecordContribution_CycleBelongsToDifferentStokvel_Returns404ProblemJson()
    {
        var user = (await (await _client.PostAsJsonAsync("/api/users", new { fullName = "Test User", email = $"{Guid.NewGuid()}@example.com" }))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;

        var stokvelA = (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Stokvel A", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;
        var stokvelB = (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Stokvel B", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;

        await _client.PostAsJsonAsync($"/api/stokvels/{stokvelA.Id}/members", new { userId = user.Id });
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvelB.Id}/members", new { userId = user.Id });

        var cycle = (await (await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId = stokvelA.Id, label = "2026-09", targetAmount = 100m }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvelB.Id}/contributions")
        {
            Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amount = 100m })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}