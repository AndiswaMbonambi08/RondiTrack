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

    [Fact]
    public async Task Role_and_cycle_status_are_enums_with_exact_spellings()
    {
        var res = await _factory.CreateAnonymousClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");

        static string[] Values(JsonElement schemas, string schema, string prop) =>
            schemas.GetProperty(schema).GetProperty("properties").GetProperty(prop)
                .GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToArray();

        var roles = new[] { "Member", "Treasurer", "Admin" };
        var statuses = new[] { "Open", "Closed", "PaidOut" };

        Assert.Equal(roles, Values(schemas, "HomeStokvelResponse", "role"));
        Assert.Equal(roles, Values(schemas, "StokvelDetailResponse", "role"));
        Assert.Equal(statuses, Values(schemas, "HomeCycleResponse", "status"));
        Assert.Equal(statuses, Values(schemas, "CycleSummaryResponse", "status"));
        Assert.Equal(statuses, Values(schemas, "StokvelCycleSummary", "status"));
    }
}