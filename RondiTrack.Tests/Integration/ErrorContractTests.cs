using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class ErrorContractTests
{
    private readonly HttpClient _client;

    public ErrorContractTests(PostgresApiFactory factory) => _client = factory.CreateClient();

    private static StringContent Raw(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task ValidationErrors_AreKeyedByCamelCaseFieldNames()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/stokvels", new { name = "", contributionAmount = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("contributionAmount", out _));
        Assert.False(errors.TryGetProperty("Name", out _));
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task NumberSentAsString_Returns400_WithNoTypeNames()
    {
        var res = await _client.PostAsync("/api/v1/stokvels", Raw("""{"name":"X","contributionAmount":"100"}"""));
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task MalformedJson_Returns400_WithNoTypeNames()
    {
        var res = await _client.PostAsync("/api/v1/stokvels", Raw("{ not json"));
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("Json", body);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesNumbersAsNumbersOnly()
    {
        var res = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var offenders = new List<string>();
        Walk(doc.RootElement, "$", offenders);

        Assert.True(offenders.Count == 0, "Number schemas that also allow strings: " + string.Join(", ", offenders));
    }

    private static void Walk(JsonElement e, string path, List<string> offenders)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array)
            {
                var names = type.EnumerateArray().Select(t => t.GetString()).ToList();
                if (names.Contains("string") && (names.Contains("integer") || names.Contains("number")))
                    offenders.Add(path);
            }
            foreach (var p in e.EnumerateObject()) Walk(p.Value, $"{path}.{p.Name}", offenders);
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in e.EnumerateArray()) Walk(item, $"{path}[{i++}]", offenders);
        }
    }

    [Fact]
    public async Task ExportOpenApiForTypeScriptClient()
    {
        var target = Environment.GetEnvironmentVariable("EXPORT_OPENAPI_TO");
        if (string.IsNullOrEmpty(target)) return;   // does nothing in normal runs

        var json = await _client.GetStringAsync("/openapi/v1.json");
        await File.WriteAllTextAsync(target, json);
    }
}