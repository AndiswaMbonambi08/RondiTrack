using System.Net;
using System.Net.Http.Json;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class ContributionResponseShapeTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task RecordedContribution_ReturnsExactCentsAndPaidStatus()
    {
        var admin = factory.CreateClient();

        var user = await admin.PostAsJsonAsync("/api/v1/users",
            new { fullName = "Shape Tester", email = $"{Guid.NewGuid():N}@test.example" });
        user.EnsureSuccessStatusCode();
        var userId = (await user.Content.ReadFromJsonAsync<UserResponse>())!.Id;

        var stokvel = await admin.PostAsJsonAsync("/api/v1/stokvels",
            new { name = "Shape Stokvel", contributionAmount = 500 });
        stokvel.EnsureSuccessStatusCode();
        var stokvelId = (await stokvel.Content.ReadFromJsonAsync<StokvelResponse>())!.Id;

        (await admin.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId }))
            .EnsureSuccessStatusCode();

        var cycle = await admin.PostAsJsonAsync("/api/v1/contribution-cycles",
            new { stokvelId, label = "2026-10", targetAmount = 500 });
        cycle.EnsureSuccessStatusCode();
        var cycleId = (await cycle.Content.ReadFromJsonAsync<ContributionCycleResponse>())!.Id;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.MintToken(userId));

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId, contributionCycleId = cycleId, amount = 123.45 })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var res = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<ContributionResponse>())!;
        Assert.Equal(12345, body.AmountCents);
        Assert.Equal(ContributionStatus.Paid, body.Status);
        Assert.Equal(cycleId, body.ContributionCycleId);
    }
}