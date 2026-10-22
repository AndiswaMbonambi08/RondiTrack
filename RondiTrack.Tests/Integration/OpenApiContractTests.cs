using System.Net;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class OpenApiContractTests
{
    private readonly PostgresApiFactory _factory;

    public OpenApiContractTests(PostgresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ProblemDetails_schema_declares_correlationId()
    {
        var res = await _factory.CreateAnonymousClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var props = doc.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("ProblemDetails").GetProperty("properties");
        Assert.True(props.TryGetProperty("correlationId", out _));
    }
}