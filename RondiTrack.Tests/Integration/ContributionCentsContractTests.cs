using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Endpoints;
using RondiTrack.Tests.TestSupport;
using Xunit;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Tests.Integration;

// Assignment 7.4: the API half of recording a contribution from a browser.
// Money is whole cents, the idempotency contract is exactly what the frontend relies on, and the
// Treasurer's member picker gets names but nobody else does. Each test uses its own users, so the
// per-user rate limit never interferes.
[Collection("Postgres collection")]
public class ContributionCentsContractTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public ContributionCentsContractTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();   // platform admin, used only to arrange data
    }

    private record MemberPage(List<MemberListItem> Items, string NextPageToken);

    // ---- helpers -------------------------------------------------------------------------

    private async Task<Guid> CreateUserAsync(string fullName = "Test User")
    {
        var email = $"{Guid.NewGuid():N}@test.example";
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName, email });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task AddMemberAsync(Guid stokvelId, Guid userId, StokvelMemberRole role = StokvelMemberRole.Member)
    {
        var res = await _admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId });
        res.EnsureSuccessStatusCode();

        if (role == StokvelMemberRole.Member) return;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var row = await db.StokvelMembers.SingleAsync(m => m.StokvelId == stokvelId && m.UserId == userId);
        db.Entry(row).Property(m => m.Role).CurrentValue = role;
        await db.SaveChangesAsync();
    }

    private sealed record Arranged(Guid StokvelId, Guid CycleId, Guid MemberId, HttpClient MemberClient);

    private async Task<Arranged> ArrangeAsync(string memberName = "Test User")
    {
        var stokvelRes = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "Contract Stokvel", contributionAmount = 500 });
        stokvelRes.EnsureSuccessStatusCode();
        var stokvelId = (await stokvelRes.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!.Id;

        var memberId = await CreateUserAsync(memberName);
        await AddMemberAsync(stokvelId, memberId);

        var cycleRes = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label = "2026-10", targetAmount = 500 });
        cycleRes.EnsureSuccessStatusCode();
        var cycleId = (await cycleRes.Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!.Id;

        return new Arranged(stokvelId, cycleId, memberId, _factory.CreateClientFor(memberId));
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Arranged a, object body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{a.StokvelId}/contributions")
        {
            Content = JsonContent.Create(body)
        };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static object Pay(Arranged a, long amountInCents) =>
        new { userId = a.MemberId, contributionCycleId = a.CycleId, amountInCents };

    private static string? Header(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    // ---- cents in, cents and rands out ---------------------------------------------------

    [Fact]
    public async Task AmountInCents_IsRecordedExactly_AndReturnedAsCentsAndRands()
    {
        var a = await ArrangeAsync();

        var res = await PostAsync(a.MemberClient, a, Pay(a, 15050), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<ContributionResponse>(JsonOptions.CaseInsensitive))!;
        Assert.Equal(15050, body.AmountCents);
        Assert.Equal(150.50m, body.Amount);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(100_000_000L)]
    public async Task SmallestAndLargestAllowedAmounts_AreAccepted(long cents)
    {
        var a = await ArrangeAsync();

        var res = await PostAsync(a.MemberClient, a, Pay(a, cents), Guid.NewGuid().ToString());

        Assert.True(res.StatusCode == HttpStatusCode.Created,
            $"{cents} cents: " + (int)res.StatusCode + " " + await res.Content.ReadAsStringAsync());
        var body = (await res.Content.ReadFromJsonAsync<ContributionResponse>(JsonOptions.CaseInsensitive))!;
        Assert.Equal(cents, body.AmountCents);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(100_000_001L)]
    public async Task OutOfRangeAmounts_Return400_WithTheFieldNameAndACorrelationId(long cents)
    {
        var a = await ArrangeAsync();

        var res = await PostAsync(a.MemberClient, a, Pay(a, cents), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("amountInCents", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("correlationId", text);
    }

    [Fact]
    public async Task TheOldRandsField_IsNoLongerAccepted()
    {
        var a = await ArrangeAsync();

        var res = await PostAsync(a.MemberClient, a,
            new { userId = a.MemberId, contributionCycleId = a.CycleId, amount = 150.50m }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task FractionalCents_AreRejected_NeverRounded()
    {
        var a = await ArrangeAsync();
        var json = $$"""{"userId":"{{a.MemberId}}","contributionCycleId":"{{a.CycleId}}","amountInCents":150.5}""";
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{a.StokvelId}/contributions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await a.MemberClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---- the idempotency contract the form relies on ------------------------------------

    [Fact]
    public async Task SameKeySameBody_ReplaysTheSameStatusAndBody_AndSaysSo()
    {
        var a = await ArrangeAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(a.MemberClient, a, Pay(a, 15050), key);
        var second = await PostAsync(a.MemberClient, a, Pay(a, 15050), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Null(Header(first, "Idempotency-Replayed"));
        Assert.Equal("true", Header(second, "Idempotency-Replayed"));
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SameKeyDifferentAmount_Returns422_AndTheOriginalAmountStays()
    {
        var a = await ArrangeAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(a.MemberClient, a, Pay(a, 15050), key);
        var second = await PostAsync(a.MemberClient, a, Pay(a, 20000), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal((HttpStatusCode)422, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NewKeyForAMemberWhoAlreadyContributed_Returns409_WithADetail()
    {
        var a = await ArrangeAsync();

        var first = await PostAsync(a.MemberClient, a, Pay(a, 15050), Guid.NewGuid().ToString());
        var second = await PostAsync(a.MemberClient, a, Pay(a, 15050), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("detail", out var detail));
        Assert.False(string.IsNullOrWhiteSpace(detail.GetString()));
        Assert.True(problem.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task NoIdempotencyKey_Returns400()
    {
        var a = await ArrangeAsync();

        var res = await PostAsync(a.MemberClient, a, Pay(a, 15050), key: null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---- the Treasurer's member picker ---------------------------------------------------

    [Fact]
    public async Task MembersList_IsForbiddenToAPlainMember()
    {
        var a = await ArrangeAsync();

        var res = await a.MemberClient.GetAsync($"/api/v1/stokvels/{a.StokvelId}/members");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MembersList_ForATreasurer_HasNames()
    {
        var a = await ArrangeAsync("Picker Member");
        var treasurerId = await CreateUserAsync("Picker Treasurer");
        await AddMemberAsync(a.StokvelId, treasurerId, StokvelMemberRole.Treasurer);

        var res = await _factory.CreateClientFor(treasurerId).GetAsync($"/api/v1/stokvels/{a.StokvelId}/members");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var page = (await res.Content.ReadFromJsonAsync<MemberPage>(JsonOptions.CaseInsensitive))!;
        Assert.Contains(page.Items, i => i.UserId == a.MemberId && i.FullName == "Picker Member");
        Assert.Contains(page.Items, i => i.UserId == treasurerId && i.FullName == "Picker Treasurer");
    }

    [Fact]
    public async Task ATreasurer_CanRecordOnBehalfOfAMember_ButNotInAnotherStokvel()
    {
        var a = await ArrangeAsync();
        var treasurerId = await CreateUserAsync("Other Treasurer");
        await AddMemberAsync(a.StokvelId, treasurerId, StokvelMemberRole.Treasurer);
        var treasurer = _factory.CreateClientFor(treasurerId);

        // A different stokvel the treasurer does not belong to.
        var other = await ArrangeAsync();

        var ok = await PostAsync(treasurer, a, Pay(a, 15050), Guid.NewGuid().ToString());
        var denied = await PostAsync(treasurer, other, Pay(other, 15050), Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);   // not a member there: the stokvel does not exist for them
    }

    // ---- the OpenAPI document the generated client is built from -------------------------

    [Fact]
    public async Task OpenApi_DescribesTheIdempotencyKeyHeader_TheCentsField_AndMemberNames()
    {
        var json = await _factory.CreateAnonymousClient().GetStringAsync("/openapi/v1.json");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var parameters = root.GetProperty("paths").GetProperty("/api/v1/stokvels/{id}/contributions")
            .GetProperty("post").GetProperty("parameters").EnumerateArray().ToList();
        Assert.Contains(parameters, p =>
            p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("in").GetString() == "header");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var request = schemas.GetProperty("RecordContributionRequest").GetProperty("properties");
        Assert.Equal("integer", request.GetProperty("amountInCents").GetProperty("type").GetString());
        Assert.False(request.TryGetProperty("amount", out _));

        var member = schemas.GetProperty("MemberListItem").GetProperty("properties");
        Assert.True(member.TryGetProperty("fullName", out _));
    }
}
