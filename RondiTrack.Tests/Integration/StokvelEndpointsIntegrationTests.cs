// Integration tests through the real pipeline for stokvels and membership.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

public class StokvelEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StokvelEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<UserResponse> CreateUserAsync() =>
        (await (await _client.PostAsJsonAsync("/api/users", new { fullName = "Test User", email = $"{Guid.NewGuid()}@example.com" }))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;

    private async Task<StokvelResponse> CreateStokvelAsync() =>
        (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Test Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;

    [Fact]
    public async Task CreateStokvel_ValidRequest_Returns201WithStokvelResponse()
    {
        var response = await _client.PostAsJsonAsync("/api/stokvels", new { name = "Holiday Savings", contributionAmount = 500m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive);
        Assert.Equal(0, body!.MemberCount);
    }

    [Fact]
    public async Task CreateStokvel_ZeroContributionAmount_Returns400ProblemJson()
    {
        var response = await _client.PostAsJsonAsync("/api/stokvels", new { name = "Bad Stokvel", contributionAmount = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddMember_ToNonexistentStokvel_Returns404ProblemJson()
    {
        var user = await CreateUserAsync();

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{Guid.NewGuid()}/members", new { userId = user.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_AlreadyAMember_Returns409ProblemJson()
    {
        var user = await CreateUserAsync();
        var stokvel = await CreateStokvelAsync();
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddMember_InactiveSeededUser_Returns409ProblemJson()
    {
        var users = await (await _client.GetAsync("/api/users")).Content.ReadFromJsonAsync<List<UserResponse>>(JsonOptions.CaseInsensitive);
        var inactiveUser = users!.First(u => !u.IsActive);
        var stokvel = await CreateStokvelAsync();

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = inactiveUser.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}