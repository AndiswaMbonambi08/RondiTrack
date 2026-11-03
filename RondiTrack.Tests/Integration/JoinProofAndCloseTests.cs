using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Tests.Integration;

// Assignment 9.1: invites and joining, proof of payment, and the close preview / close.
[Collection("Postgres collection")]
public class JoinProofAndCloseTests
{
    private const int Max = 5 * 1024 * 1024;
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _admin;

    public JoinProofAndCloseTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
    }

    // ---- helpers ------------------------------------------------------------------------

    private async Task<Guid> CreateUserAsync(string fullName)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/users", new { fullName, email = $"{Guid.NewGuid():N}@test.example" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task<Guid> CreateStokvelAsync()
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/stokvels", new { name = "Nine One Stokvel", contributionAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!.Id;
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

    private async Task<Guid> CreateCycleAsync(Guid stokvelId)
    {
        var res = await _admin.PostAsJsonAsync("/api/v1/contribution-cycles", new { stokvelId, label = "2026-10", targetAmount = 500 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!.Id;
    }

    private async Task<Guid> SeedContributionAsync(Guid stokvelId, Guid userId, Guid cycleId, decimal amount = 500m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var c = new Contribution(stokvelId, userId, cycleId, amount, DateTime.UtcNow.AddMinutes(-5), ContributionStatus.Paid);
        db.Contributions.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private sealed record Setup(Guid StokvelId, Guid CycleId, Guid TreasurerId, HttpClient Treasurer);

    private async Task<Setup> ArrangeAsync()
    {
        var stokvelId = await CreateStokvelAsync();
        var treasurerId = await CreateUserAsync("Nine Treasurer");
        await AddMemberAsync(stokvelId, treasurerId, StokvelMemberRole.Treasurer);
        var cycleId = await CreateCycleAsync(stokvelId);
        return new Setup(stokvelId, cycleId, treasurerId, _factory.CreateClientFor(treasurerId));
    }

    private static byte[] Png(int size = 200)
    {
        var b = new byte[size];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        return b;
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    private static string ProofUrl(Guid stokvelId, Guid contributionId) =>
        $"/api/v1/stokvels/{stokvelId}/contributions/{contributionId}/proof";

    private static async Task<JsonElement> JsonOf(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<string> CreateInviteAsync(Setup a)
    {
        var res = await a.Treasurer.PostAsJsonAsync($"/api/v1/stokvels/{a.StokvelId}/invites", new { expiresInDays = 7 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await JsonOf(res)).GetProperty("code").GetString()!;
    }

    private Task<HttpResponseMessage> CloseAsync(HttpClient client, Setup a, string version, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{a.StokvelId}/cycles/{a.CycleId}/close")
        {
            Content = JsonContent.Create(new { version })
        };
        req.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(req);
    }

    private async Task<JsonElement> PreviewAsync(Setup a)
    {
        var res = await a.Treasurer.GetAsync($"/api/v1/stokvels/{a.StokvelId}/cycles/{a.CycleId}/close-preview");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await JsonOf(res);
    }

    // ---- invites and joining -------------------------------------------------------------

    [Fact]
    public async Task Preview_SendsOnlyWhatAPersonNeedsToDecide()
    {
        var a = await ArrangeAsync();
        var code = await CreateInviteAsync(a);
        var stranger = await CreateUserAsync("Stranger");

        var res = await _factory.CreateClientFor(stranger).GetAsync($"/api/v1/invites/{code}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await JsonOf(res);
        var names = json.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "alreadyMember", "contributionCents", "cycleEndDay", "memberCount", "rules", "schedule", "stokvelId", "stokvelName" }, names);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("stokvelId").ValueKind);
        Assert.False(json.GetProperty("alreadyMember").GetBoolean());
        Assert.Equal(50000, json.GetProperty("contributionCents").GetInt64());
    }

    [Fact]
    public async Task UnknownCode_Is404_ExpiredCode_Is410_AndOnlyTreasurersCreateInvites()
    {
        var a = await ArrangeAsync();
        var code = await CreateInviteAsync(a);
        var member = await CreateUserAsync("Plain Member");
        await AddMemberAsync(a.StokvelId, member);

        var unknown = await _factory.CreateClientFor(member).GetAsync("/api/v1/invites/ZZZZZZZZZZ");
        var notTreasurer = await _factory.CreateClientFor(member)
            .PostAsJsonAsync($"/api/v1/stokvels/{a.StokvelId}/invites", new { expiresInDays = 7 });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            var invite = await db.StokvelInvites.SingleAsync(i => i.Code == code);
            db.Entry(invite).Property(i => i.ExpiresAtUtc).CurrentValue = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        var expired = await _factory.CreateClientFor(member).GetAsync($"/api/v1/invites/{code}");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notTreasurer.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Equal("invite-expired", (await JsonOf(expired)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Join_MakesOneMembership_AndARepeatIsAlreadyAMember()
    {
        var a = await ArrangeAsync();
        var code = await CreateInviteAsync(a);
        var newcomer = await CreateUserAsync("Newcomer");
        var client = _factory.CreateClientFor(newcomer);

        var first = await client.PostAsync($"/api/v1/invites/{code}/join", null);
        var second = await client.PostAsync($"/api/v1/invites/{code}/join", null);
        var detail = await client.GetAsync($"/api/v1/stokvels/{a.StokvelId}");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await JsonOf(second);
        Assert.Equal("already-member", problem.GetProperty("code").GetString());
        Assert.Equal(a.StokvelId.ToString(), problem.GetProperty("stokvelId").GetString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(m => m.StokvelId == a.StokvelId && m.UserId == newcomer));
    }

    // ---- proof of payment ------------------------------------------------------------------

    [Fact]
    public async Task Upload_JudgesTheBytes_AndEnforcesTheLimit()
    {
        var a = await ArrangeAsync();
        var member = await CreateUserAsync("Uploader");
        await AddMemberAsync(a.StokvelId, member);
        var contribution = await SeedContributionAsync(a.StokvelId, member, a.CycleId);
        var client = _factory.CreateClientFor(member);
        var url = ProofUrl(a.StokvelId, contribution);

        var good = await client.PostAsync(url, Form(Png(), "slip.png", "image/png"));
        var fakePdf = await client.PostAsync(url, Form(Encoding.UTF8.GetBytes("this is plain text"), "statement.pdf", "application/pdf"));
        var tooBig = await client.PostAsync(url, Form(Png(Max + 1), "big.png", "image/png"));
        var justUnder = await client.PostAsync(url, Form(Png(Max), "edge.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fakePdf.StatusCode);
        Assert.Equal("file-type", (await JsonOf(fakePdf)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        Assert.Equal(HttpStatusCode.OK, justUnder.StatusCode);
    }

    [Fact]
    public async Task Download_OnlyTheUploaderAndThisStokvelsTreasurers()
    {
        var a = await ArrangeAsync();
        var other = await ArrangeAsync();   // a second stokvel with its own treasurer
        var uploader = await CreateUserAsync("Uploader");
        var bystander = await CreateUserAsync("Bystander");
        await AddMemberAsync(a.StokvelId, uploader);
        await AddMemberAsync(a.StokvelId, bystander);
        var contribution = await SeedContributionAsync(a.StokvelId, uploader, a.CycleId);
        var up = await _factory.CreateClientFor(uploader)
            .PostAsync(ProofUrl(a.StokvelId, contribution), Form(Png(), "slip.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        var url = ProofUrl(a.StokvelId, contribution);

        var asUploader = await _factory.CreateClientFor(uploader).GetAsync(url);
        var asTreasurer = await a.Treasurer.GetAsync(url);
        var asBystander = await _factory.CreateClientFor(bystander).GetAsync(url);
        var asOtherTreasurer = await other.Treasurer.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, asUploader.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asTreasurer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asBystander.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOtherTreasurer.StatusCode);
        Assert.Contains("nosniff", asUploader.Headers.GetValues("X-Content-Type-Options"));
        Assert.True(asUploader.Headers.CacheControl!.Private);
        Assert.True(asUploader.Headers.CacheControl.NoStore);
        Assert.Equal("attachment", asUploader.Content.Headers.ContentDisposition!.DispositionType);
    }

    // ---- closing a cycle ---------------------------------------------------------------------

    [Fact]
    public async Task ClosePreview_ListsWhoOwes_AndOnlyTreasurersMayAsk()
    {
        var a = await ArrangeAsync();
        var payer = await CreateUserAsync("Payer");
        var slacker = await CreateUserAsync("Slacker");
        await AddMemberAsync(a.StokvelId, payer);
        await AddMemberAsync(a.StokvelId, slacker);
        await SeedContributionAsync(a.StokvelId, payer, a.CycleId);

        var preview = await PreviewAsync(a);
        var memberClient = _factory.CreateClientFor(payer);
        var forbidden = await memberClient.GetAsync($"/api/v1/stokvels/{a.StokvelId}/cycles/{a.CycleId}/close-preview");

        // The treasurer and the slacker have not paid.
        Assert.Equal(2, preview.GetProperty("items").GetArrayLength());
        Assert.Equal(100000, preview.GetProperty("totalArrearsCents").GetInt64());
        Assert.Contains(preview.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("memberName").GetString() == "Slacker" && i.GetProperty("shortfallCents").GetInt64() == 50000);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Close_RecordsExactlyWhatWasPreviewed_OnceEvenWhenSentTwice()
    {
        var a = await ArrangeAsync();
        var slacker = await CreateUserAsync("Slacker");
        await AddMemberAsync(a.StokvelId, slacker);
        var preview = await PreviewAsync(a);
        var version = preview.GetProperty("version").GetString()!;
        var key = Guid.NewGuid().ToString();

        var first = await CloseAsync(a.Treasurer, a, version, key);
        var again = await CloseAsync(a.Treasurer, a, version, key);
        var otherKey = await CloseAsync(a.Treasurer, a, version, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("true", again.Headers.GetValues("Idempotency-Replayed"));
        Assert.Equal(HttpStatusCode.Conflict, otherKey.StatusCode);
        Assert.Equal("already-closed", (await JsonOf(otherKey)).GetProperty("code").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(2, await db.Arrears.CountAsync(x => x.ContributionCycleId == a.CycleId));
    }

    [Fact]
    public async Task Close_WithAStalePreview_Is409_AndRecordsNothing()
    {
        var a = await ArrangeAsync();
        var slacker = await CreateUserAsync("Slacker");
        await AddMemberAsync(a.StokvelId, slacker);
        var version = (await PreviewAsync(a)).GetProperty("version").GetString()!;

        await SeedContributionAsync(a.StokvelId, slacker, a.CycleId);   // recorded after the preview
        var res = await CloseAsync(a.Treasurer, a, version, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("stale-preview", (await JsonOf(res)).GetProperty("code").GetString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(0, await db.Arrears.CountAsync(x => x.ContributionCycleId == a.CycleId));
        Assert.Null((await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == a.CycleId)).ClosedAtUtc);
    }

    [Fact]
    public async Task Close_ByAMember_Is403_AndAClosedCycleLocksTheProof()
    {
        var a = await ArrangeAsync();
        var member = await CreateUserAsync("Plain");
        await AddMemberAsync(a.StokvelId, member);
        var contribution = await SeedContributionAsync(a.StokvelId, member, a.CycleId);
        var memberClient = _factory.CreateClientFor(member);

        var forbidden = await CloseAsync(memberClient, a, "whatever", Guid.NewGuid().ToString());
        var version = (await PreviewAsync(a)).GetProperty("version").GetString()!;
        var closed = await CloseAsync(a.Treasurer, a, version, Guid.NewGuid().ToString());
        var upload = await memberClient.PostAsync(ProofUrl(a.StokvelId, contribution), Form(Png(), "slip.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
        Assert.Equal("proof-locked", (await JsonOf(upload)).GetProperty("code").GetString());
    }
}