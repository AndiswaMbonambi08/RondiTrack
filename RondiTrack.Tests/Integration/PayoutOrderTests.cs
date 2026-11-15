using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class PayoutOrderTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public PayoutOrderTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
    }

    private async Task<Guid> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName = "Test User", email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task<Guid> CreateStokvelAsync(string name)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name, contributionAmount = 500 });
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
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.MintToken(userId));
        return client;
    }

    private async Task<(Guid Stokvel, Guid Cycle, List<Guid> Members)> SeedAsync(string name, int memberCount)
    {
        var stokvel = await CreateStokvelAsync(name);
        var members = new List<Guid>();
        for (var i = 0; i < memberCount; i++)
        {
            var user = await CreateUserAsync();
            await AddMemberAsync(stokvel, user);
            members.Add(user);
            await Task.Delay(25); // distinct join times, so the rotation order is the order added
        }
        var cycle = await CreateCycleAsync(stokvel, "2026-11");
        return (stokvel, cycle, members);
    }

    private async Task PayAsync(Guid stokvel, Guid cycle, Guid recipient)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        db.Payouts.Add(new Payout(stokvel, cycle, recipient, 1500m));
        await db.SaveChangesAsync();
    }

    private static string Url(object stokvel, object cycle) =>
        $"/api/v1/stokvels/{stokvel}/cycles/{cycle}/payout-order";

    [Fact]
    public async Task Member_GetsTheOrderAsTheServerDecidesIt()
    {
        var (stokvel, cycle, members) = await SeedAsync("Order Stokvel", 3);
        await PayAsync(stokvel, cycle, members[0]);

        var res = await ClientFor(members[1]).GetAsync(Url(stokvel, cycle));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        var order = JsonSerializer.Deserialize<PayoutOrderResponse>(raw, JsonOptions.CaseInsensitive)!;

        Assert.Equal(members, order.Items.Select(i => i.MemberId).ToList());
        Assert.Equal(new[] { 1, 2, 3 }, order.Items.Select(i => i.Position));
        Assert.Equal(new[] { "Paid", "Next", "Waiting" }, order.Items.Select(i => i.Status));
        Assert.All(order.Items, i => Assert.Equal(150000L, i.AmountCents));
        Assert.NotNull(order.Items[0].PayoutDate);
        Assert.Null(order.Items[1].PayoutDate);
        Assert.Contains("\"status\":\"Next\"", raw);
    }

    [Fact]
    public async Task NoPayoutYet_FirstMemberIsNext()
    {
        var (stokvel, cycle, members) = await SeedAsync("Fresh Stokvel", 2);

        var res = await ClientFor(members[0]).GetAsync(Url(stokvel, cycle));
        var order = (await res.Content.ReadFromJsonAsync<PayoutOrderResponse>(JsonOptions.CaseInsensitive))!;

        Assert.Equal(new[] { "Next", "Waiting" }, order.Items.Select(i => i.Status));
    }

    [Fact]
    public async Task EveryonePaid_NobodyIsNext()
    {
        var (stokvel, cycle, members) = await SeedAsync("Done Stokvel", 2);
        foreach (var m in members) await PayAsync(stokvel, cycle, m);

        var res = await ClientFor(members[0]).GetAsync(Url(stokvel, cycle));
        var order = (await res.Content.ReadFromJsonAsync<PayoutOrderResponse>(JsonOptions.CaseInsensitive))!;

        Assert.All(order.Items, i => Assert.Equal("Paid", i.Status));
    }

    [Fact]
    public async Task NonMember_GetsNotFound_AsProblemJson()
    {
        var (stokvel, cycle, _) = await SeedAsync("Private Stokvel", 2);
        var outsider = await CreateUserAsync();

        var res = await ClientFor(outsider).GetAsync(Url(stokvel, cycle));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CycleOfAnotherStokvel_GetsNotFound()
    {
        var (stokvelA, _, membersA) = await SeedAsync("Stokvel A", 2);
        var (_, cycleB, _) = await SeedAsync("Stokvel B", 2);

        var res = await ClientFor(membersA[0]).GetAsync(Url(stokvelA, cycleB));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UnknownCycle_GetsNotFound()
    {
        var (stokvel, _, members) = await SeedAsync("Unknown Cycle", 2);

        var res = await ClientFor(members[0]).GetAsync(Url(stokvel, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MalformedIds_GetNotFound()
    {
        var user = await CreateUserAsync();

        var res = await ClientFor(user).GetAsync("/api/v1/stokvels/not-a-guid/cycles/also-not/payout-order");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}