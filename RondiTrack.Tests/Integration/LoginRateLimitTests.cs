using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class LoginRateLimitTests : IDisposable
{
    private const string FrontendOrigin = "http://localhost:3000";
    private const int Limit = 3;
    private const int WindowSeconds = 60;

    private readonly WebApplicationFactory<Program> _app;

    public LoginRateLimitTests(PostgresApiFactory factory)
    {
        // WithWebHostBuilder builds a new host, so this test gets its own fresh limiter.
        _app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimit:Login:PermitLimit", Limit.ToString());
            b.UseSetting("RateLimit:Login:WindowSeconds", WindowSeconds.ToString());
            b.UseSetting("Cors:AllowedOrigins:0", FrontendOrigin);
        });
    }

    public void Dispose() => _app.Dispose();

    private static HttpRequestMessage LoginAttempt()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "nobody@example.com", password = "wrong-password-123" })
        };
        request.Headers.TryAddWithoutValidation("Origin", FrontendOrigin);
        return request;
    }

    [Fact]
    public async Task Login_OverTheLimit_Returns429_WithRetryAfter_ProblemJson_AndCorsHeaders()
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        for (var i = 0; i < Limit; i++)
        {
            var allowed = await client.SendAsync(LoginAttempt());
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var res = await client.SendAsync(LoginAttempt());

        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);

        var retryAfter = res.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter!.Value.TotalSeconds, 1, WindowSeconds);

        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(429, root.GetProperty("status").GetInt32());
        Assert.Equal("Too Many Requests", root.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));

        // The browser must be allowed to read the 429 and its Retry-After.
        Assert.True(res.Headers.TryGetValues("Access-Control-Allow-Origin", out var origin));
        Assert.Equal(FrontendOrigin, string.Join(",", origin!));
    }
}