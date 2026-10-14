using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

// Drives the real register, login, refresh and logout endpoints. The refresh cookie is Secure and the
// test server is plain http, so a cookie jar would never send it back. The tests read Set-Cookie and
// send the cookie by hand, which also lets them replay an old token on purpose.
[Collection("Postgres collection")]
public class AuthFlowTests
{
    private const string Password = "a-long-test-password-123";
    private const string CookieName = "refresh_token";

    private readonly PostgresApiFactory _factory;

    public AuthFlowTests(PostgresApiFactory factory) => _factory = factory;

    // ---- helpers -------------------------------------------------------------------------

    private async Task<string> RegisterAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _factory.CreateAnonymousClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Auth Test", email, password = Password });
        res.EnsureSuccessStatusCode();
        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private static string? ExtractRefreshToken(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        var header = values.FirstOrDefault(v => v.StartsWith(CookieName + "="));
        return header?.Split(';')[0][(CookieName.Length + 1)..];
    }

    private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (token is not null) request.Headers.Add("Cookie", $"{CookieName}={token}");
        return client.SendAsync(request);
    }

    private async Task<string> LoginAndGetTokenAsync(HttpClient client)
    {
        var email = await RegisterAsync();
        var res = await LoginAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return ExtractRefreshToken(res)!;
    }

    private static async Task<(int Status, string? Title, string? Detail, string? Type)> ReadProblemAsync(
        HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        string? Get(string name) => root.TryGetProperty(name, out var p) ? p.ToString() : null;
        return ((int)res.StatusCode, Get("title"), Get("detail"), Get("type"));
    }

    // ---- login ---------------------------------------------------------------------------

    [Fact]
    public async Task Login_SetsHardenedRefreshCookie_AndBodyHasNoRefreshToken()
    {
        var client = _factory.CreateAnonymousClient();
        var email = await RegisterAsync();

        var res = await LoginAsync(client, email, Password);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var token = ExtractRefreshToken(res);
        Assert.False(string.IsNullOrEmpty(token));

        var setCookie = string.Join(";", res.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("httponly", setCookie);
        Assert.Contains("secure", setCookie);
        Assert.Contains("samesite=strict", setCookie);
        Assert.Contains("path=/api/v1/auth", setCookie);

        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token!, body);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnIdenticalProblemDetails()
    {
        var client = _factory.CreateAnonymousClient();
        var email = await RegisterAsync();

        var wrongPassword = await LoginAsync(client, email, "not-the-right-password-1");
        var unknownEmail = await LoginAsync(client, $"{Guid.NewGuid():N}@test.example", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("application/problem+json", wrongPassword.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/problem+json", unknownEmail.Content.Headers.ContentType?.MediaType);

        // correlationId legitimately differs per request, so compare the rest of the shape.
        Assert.Equal(await ReadProblemAsync(wrongPassword), await ReadProblemAsync(unknownEmail));
    }

    // ---- refresh -------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_RotatesToken_AndNewTokenWorks()
    {
        var client = _factory.CreateAnonymousClient();
        var first = await LoginAndGetTokenAsync(client);

        var refreshed = await PostWithCookieAsync(client, "/api/v1/auth/refresh", first);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = ExtractRefreshToken(refreshed);
        Assert.False(string.IsNullOrEmpty(second));
        Assert.NotEqual(first, second);

        var body = await refreshed.Content.ReadAsStringAsync();
        Assert.DoesNotContain(second!, body);

        var again = await PostWithCookieAsync(client, "/api/v1/auth/refresh", second);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReplayOfUsedToken_Returns401_AndNewerTokenIsAlsoRejected()
    {
        var client = _factory.CreateAnonymousClient();
        var first = await LoginAndGetTokenAsync(client);

        var rotated = await PostWithCookieAsync(client, "/api/v1/auth/refresh", first);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = ExtractRefreshToken(rotated);

        var replay = await PostWithCookieAsync(client, "/api/v1/auth/refresh", first);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("application/problem+json", replay.Content.Headers.ContentType?.MediaType);

        // Reuse detection revoked the whole family, so the newer token is dead too.
        var newer = await PostWithCookieAsync(client, "/api/v1/auth/refresh", second);
        Assert.Equal(HttpStatusCode.Unauthorized, newer.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithNoCookie_Returns401()
    {
        var res = await PostWithCookieAsync(_factory.CreateAnonymousClient(), "/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    // ---- logout --------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_AfterLogout_Returns401()
    {
        var client = _factory.CreateAnonymousClient();
        var token = await LoginAndGetTokenAsync(client);

        var logout = await PostWithCookieAsync(client, "/api/v1/auth/logout", token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await PostWithCookieAsync(client, "/api/v1/auth/refresh", token);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_IsIdempotent()
    {
        var client = _factory.CreateAnonymousClient();
        var token = await LoginAndGetTokenAsync(client);

        var first = await PostWithCookieAsync(client, "/api/v1/auth/logout", token);
        var second = await PostWithCookieAsync(client, "/api/v1/auth/logout", token);
        var noCookie = await PostWithCookieAsync(client, "/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, noCookie.StatusCode);
    }

    // ---- versioned path (new for 6.3) ---------------------------------------------------

    [Fact]
    public async Task Refresh_AtVersionedPath_Succeeds_AndCookieStaysScopedToVersionedPath()
    {
        var client = _factory.CreateAnonymousClient();
        var first = await LoginAndGetTokenAsync(client);

        var refreshed = await PostWithCookieAsync(client, "/api/v1/auth/refresh", first);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = ExtractRefreshToken(refreshed);
        Assert.False(string.IsNullOrEmpty(second));

        // The rotated cookie must keep the moved Path, or the browser would stop sending it.
        var setCookie = string.Join(";", refreshed.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Matches(@"path=/api/v1/auth(;|$)", setCookie);
    }

    [Fact]
    public async Task Refresh_AtOldUnversionedPath_IsNotServed()
    {
        var client = _factory.CreateAnonymousClient();
        var token = await LoginAndGetTokenAsync(client);

        var res = await PostWithCookieAsync(client, "/api/auth/refresh", token);

        // No route matches the old path. Depending on the fallback authorization policy an anonymous
        // caller sees 404 (no route) or 401 (policy runs first). Either way the old path must not refresh.
        Assert.True(
            res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
            $"Expected 404 or 401 but got {(int)res.StatusCode}.");
        Assert.False(res.Headers.Contains("Set-Cookie"));
    }
}