using System.Net;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class ObservabilityTests(PostgresApiFactory factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Probes_return_200_without_a_token(string path)
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_401_returns_the_sent_trace_id_as_correlationId()
    {
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        var client = factory.CreateAnonymousClient();
        using var request = WithTrace(HttpMethod.Get, "/api/stokvels", traceId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(traceId, await CorrelationIdOf(response));
    }

    [Fact]
    public async Task Non_member_404_returns_the_sent_trace_id_as_correlationId()
    {
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        // Not a platform admin, and a member of nothing: the authorization handler answers 404.
        var outsider = factory.CreateClientFor(Guid.NewGuid());
        using var request = WithTrace(HttpMethod.Get, $"/api/stokvels/{Guid.NewGuid()}/members", traceId);

        var response = await outsider.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(traceId, await CorrelationIdOf(response));
    }

    private static HttpRequestMessage WithTrace(HttpMethod method, string path, string traceId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");
        return request;
    }

    private static async Task<string?> CorrelationIdOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("correlationId").GetString();
    }
}