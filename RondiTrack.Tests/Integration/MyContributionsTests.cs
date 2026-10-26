using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class MyContributionsTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public MyContributionsTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
    }

    private record Page(List<ContributionItemResponse> Items, string NextPageToken);

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task<Guid> CreateStokvelAsync()
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "Mine Stokvel", contributionAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task AddMemberAsync(Guid stokvelId, Guid userId)
    {
        var res = await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId });
        res.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateCycleAsync(Guid stokvelId, string label)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label, targetAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private HttpClient ClientFor(Guid userId)
    {
        var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _factory.MintToken(userId));
        return client;
    }

    private async Task<ContributionResponse> PayAsync(Guid userId, Guid stokvelId, Guid cycleId, long amountInCents)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId, contributionCycleId = cycleId, amountInCents })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var res = await ClientFor(userId).SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ContributionResponse>(JsonOptions.CaseInsensitive))!;
    }

    [Fact]
    public async Task List_ReturnsIntegerCentsStatusAndCycleLabel()
    {
        var userId = await CreateUserAsync();
        var stokvelId = await CreateStokvelAsync();
        await AddMemberAsync(stokvelId, userId);
        var cycleId = await CreateCycleAsync(stokvelId, "2026-10");
        var paid = await PayAsync(userId, stokvelId, cycleId, 50000);

        var res = await ClientFor(userId).GetAsync($"/api/v1/stokvels/{stokvelId}/contributions");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var page = (await res.Content.ReadFromJsonAsync<Page>(JsonOptions.CaseInsensitive))!;
        var item = Assert.Single(page.Items);
        Assert.Equal(paid.Id, item.Id);
        Assert.Equal(50000, item.AmountCents);
        Assert.Equal(ContributionStatus.Paid, item.Status);
        Assert.Equal("2026-10", item.CycleLabel);
        Assert.Equal(cycleId, item.CycleId);
        Assert.Equal("", page.NextPageToken);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCallersOwnContributions()
    {
        var me = await CreateUserAsync();
        var other = await CreateUserAsync();
        var stokvelId = await CreateStokvelAsync();
        await AddMemberAsync(stokvelId, me);
        await AddMemberAsync(stokvelId, other);
        var cycleId = await CreateCycleAsync(stokvelId, "2026-10");
        var mine = await PayAsync(me, stokvelId, cycleId, 50000);
        await PayAsync(other, stokvelId, cycleId, 25000);

        var body = await ClientFor(me).GetStringAsync($"/api/v1/stokvels/{stokvelId}/contributions");
        var page = JsonSerializer.Deserialize<Page>(body, JsonOptions.CaseInsensitive)!;

        Assert.Equal(mine.Id, Assert.Single(page.Items).Id);
        Assert.DoesNotContain(other.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var res = await _factory.CreateAnonymousClient()
            .GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}/contributions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task OpenApi_PublishesAmountCentsAsInteger()
    {
        var json = await _factory.CreateAnonymousClient().GetStringAsync("/openapi/v1.json");
        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("ContributionItemResponse").GetProperty("properties");
        Assert.Equal("integer", props.GetProperty("amountCents").GetProperty("type").GetString());
    }
}
