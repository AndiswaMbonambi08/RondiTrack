using RondiTrack.Tests.TestSupport;
// Integration tests for ContributionCycle CRUD and contribution recording.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class ContributionCycleAndContributionTests
{
    private readonly HttpClient _client;

    public ContributionCycleAndContributionTests(PostgresApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserResponse> CreateUserAsync() =>
        (await (await _client.PostAsJsonAsync("/api/users", new { fullName = "Test User", email = $"{Guid.NewGuid()}@example.com" }))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;

    private async Task<StokvelResponse> CreateStokvelAsync() =>
        (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Test Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;

    private async Task<ContributionCycleResponse> CreateCycleAsync(Guid stokvelId, string label = "2026-09") =>
        (await (await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId, label, targetAmount = 100m }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

    [Fact]
    public async Task CreateContributionCycle_ValidRequest_Returns201()
    {
        var stokvel = await CreateStokvelAsync();

        var response = await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId = stokvel.Id, label = "2026-09", targetAmount = 100m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateContributionCycle_ZeroTargetAmount_Returns400ProblemJson()
    {
        var stokvel = await CreateStokvelAsync();

        var response = await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId = stokvel.Id, label = "2026-09", targetAmount = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContributionCycle_NonexistentStokvel_Returns404ProblemJson()
    {
        var response = await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId = Guid.NewGuid(), label = "2026-09", targetAmount = 100m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordContribution_ValidMemberAndCycle_Returns201()
{
    var user = await CreateUserAsync();
    var stokvel = await CreateStokvelAsync();
    var add = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });
    Assert.True(add.IsSuccessStatusCode, "AddMember: " + (int)add.StatusCode + " " + await add.Content.ReadAsStringAsync());
    var cycle = await CreateCycleAsync(stokvel.Id);

    var request = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions")
    {
        Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amount = 100m })
    };
    request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

    var response = await _client.SendAsync(request);

    Assert.True(response.StatusCode == HttpStatusCode.Created,
        "Record: " + (int)response.StatusCode + " " + await response.Content.ReadAsStringAsync());
}
    [Fact]
    public async Task RecordContribution_MissingIdempotencyKey_Returns400ProblemJson()
    {
        var user = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync();
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });
        var cycle = await CreateCycleAsync(stokvel.Id);

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/contributions", new { userId = user.Id, contributionCycleId = cycle.Id, amount = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordContribution_NegativeAmount_Returns400ProblemJson()
    {
        var user = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync();
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });
        var cycle = await CreateCycleAsync(stokvel.Id);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions")
        {
            Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amount = -50m })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordContribution_SameUserSameCycleDifferentIdempotencyKeys_Returns409ProblemJson()
    {
        var user = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync();
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });
        var cycle = await CreateCycleAsync(stokvel.Id);

        var body = new { userId = user.Id, contributionCycleId = cycle.Id, amount = 100m };

        var first = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions") { Content = JsonContent.Create(body) };
        first.Headers.Add("Idempotency-Key", "key-a");
        await _client.SendAsync(first);

        var second = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions") { Content = JsonContent.Create(body) };
        second.Headers.Add("Idempotency-Key", "key-b");
        var response = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
