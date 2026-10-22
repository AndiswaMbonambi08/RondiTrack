using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class MeEndpointTests
{
    private const string Password = "a-long-test-password-123";
    private readonly PostgresApiFactory _factory;

    public MeEndpointTests(PostgresApiFactory factory) => _factory = factory;

    private async Task<(string Email, string AccessToken)> SignInAsync()
    {
        var client = _factory.CreateAnonymousClient();
        var email = $"{Guid.NewGuid():N}@test.example";
        (await client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Me Test", email, password = Password })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (email, doc.RootElement.GetProperty("accessToken").GetString()!);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsOwnProfile_AndNoSecrets()
    {
        var (email, token) = await SignInAsync();
        var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
        Assert.Contains(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var res = await _factory.CreateAnonymousClient().GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}