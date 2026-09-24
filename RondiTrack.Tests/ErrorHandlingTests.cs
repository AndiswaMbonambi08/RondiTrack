// Negative-path tests proving the error shape holds: a malformed request, a not-found,
// and a business-rule violation each return the right status code and problem+json.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RondiTrack.Tests;

public class ErrorHandlingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ErrorHandlingTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatingUser_WithMissingFullName_Returns400ProblemJson()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new { fullName = "", email = "test@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GettingUnknownUser_Returns404ProblemJson()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddingSameMemberTwice_Returns409ProblemJson()
    {
        var stokvels = await _client.GetFromJsonAsync<List<StokvelDto>>("/api/stokvels");
        var stokvel = stokvels!.First();

        var members = await _client.GetFromJsonAsync<List<Guid>>($"/api/stokvels/{stokvel.Id}/members");
        var existingMemberId = members!.First();

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = existingMemberId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private record StokvelDto(Guid Id, string Name, decimal ContributionAmount, int MemberCount);
}