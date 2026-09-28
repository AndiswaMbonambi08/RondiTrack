// Integration tests through the real pipeline (validation, endpoint, exception handler).
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

public class UserEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UserEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ValidRequest_Returns201WithUserResponse()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new { fullName = "Zanele Khumalo", email = "zanele@example.com" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive);
        Assert.Equal("Zanele Khumalo", body!.FullName);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task CreateUser_MissingFullName_Returns400ProblemJson()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new { fullName = "", email = "test@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetUser_UnknownId_Returns404ProblemJson()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}