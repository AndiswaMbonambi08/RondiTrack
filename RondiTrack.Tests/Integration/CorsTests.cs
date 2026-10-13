using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

// Browsers send preflights and Origin headers that curl never does, so these tests send them by hand.
[Collection("Postgres collection")]
public class CorsTests : IDisposable
{
    private const string FrontendOrigin = "http://localhost:3000";
    private const string OtherOrigin = "http://evil.example";

    private readonly PostgresApiFactory _factory;
    private readonly WebApplicationFactory<Program> _app;

    public CorsTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _app = factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", FrontendOrigin));
    }

    public void Dispose() => _app.Dispose();

    // ---- helpers -------------------------------------------------------------------------

    private static string? Header(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    private HttpClient AnonymousClient()
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;   // a real preflight carries no token
        return client;
    }

    private HttpClient OrdinaryUserClient()
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.MintToken(Guid.NewGuid()));
        return client;
    }

    private static HttpRequestMessage Preflight(string url, string origin, string method, string requestHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", method);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", requestHeaders);
        return request;
    }

    private static HttpRequestMessage WithOrigin(HttpMethod method, string url, string origin)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        return request;
    }

    // ---- preflight -----------------------------------------------------------------------

    [Fact]
    public async Task Preflight_FromFrontend_WithIdempotencyKey_IsAllowedWithCredentials_AndNeedsNoToken()
    {
        var url = $"/api/v1/stokvels/{Guid.NewGuid()}/contributions";

        var res = await AnonymousClient().SendAsync(
            Preflight(url, FrontendOrigin, "POST", "idempotency-key,content-type,authorization"));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(FrontendOrigin, Header(res, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(res, "Access-Control-Allow-Credentials"));
        Assert.Contains("idempotency-key", Header(res, "Access-Control-Allow-Headers"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("POST", Header(res, "Access-Control-Allow-Methods"));
    }

    [Fact]
    public async Task Preflight_FromAnyOtherOrigin_GetsNoCorsHeaders()
    {
        var url = $"/api/v1/stokvels/{Guid.NewGuid()}/contributions";

        var res = await AnonymousClient().SendAsync(
            Preflight(url, OtherOrigin, "POST", "idempotency-key,content-type"));

        Assert.Null(Header(res, "Access-Control-Allow-Origin"));
        Assert.Null(Header(res, "Access-Control-Allow-Credentials"));
        Assert.Null(Header(res, "Access-Control-Allow-Headers"));
    }

    // ---- error responses carry CORS headers ---------------------------------------------

    [Fact]
    public async Task StokvelScoped404_FromAuthorization_CarriesCorsHeaders_AndExposesRetryAfter()
    {
        // An ordinary user who belongs to no stokvel: the policy answers 404 without throwing.
        var res = await OrdinaryUserClient().SendAsync(
            WithOrigin(HttpMethod.Get, $"/api/v1/stokvels/{Guid.NewGuid()}/members", FrontendOrigin));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(FrontendOrigin, Header(res, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(res, "Access-Control-Allow-Credentials"));
        Assert.Contains("Retry-After", Header(res, "Access-Control-Expose-Headers"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Thrown404_GoesThroughExceptionHandler_AndStillCarriesCorsHeaders()
    {
        // The default test client is a platform admin, so the policy passes and the endpoint throws NotFoundException.
        var res = await _app.CreateClient().SendAsync(
            WithOrigin(HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}", FrontendOrigin));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(FrontendOrigin, Header(res, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(res, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Forbidden403_CarriesCorsHeaders()
    {
        // Listing every stokvel is platform-admin only, so an ordinary user gets 403.
        var res = await OrdinaryUserClient().SendAsync(
            WithOrigin(HttpMethod.Get, "/api/v1/stokvels", FrontendOrigin));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(FrontendOrigin, Header(res, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ErrorResponse_ForUnknownOrigin_CarriesNoCorsHeaders()
    {
        var res = await OrdinaryUserClient().SendAsync(
            WithOrigin(HttpMethod.Get, $"/api/v1/stokvels/{Guid.NewGuid()}/members", OtherOrigin));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Null(Header(res, "Access-Control-Allow-Origin"));
    }
}