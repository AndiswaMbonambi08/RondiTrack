using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using RondiTrack.Tests.Integration;
using Xunit;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class PayoutMetricTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Processing_a_payout_records_a_success_measurement_and_a_repeat_records_a_failed_one()
    {
        long success = 0, failed = 0;

        // The Meter is static and shared by the whole test process, so this counts only what
        // arrives while the listener is running and asserts "at least", not "exactly".
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "RondiTrack" && instrument.Name == "ronditrack.payouts.processed")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key != "outcome") continue;
                if (tag.Value as string == "success") Interlocked.Add(ref success, measurement);
                if (tag.Value as string == "failed") Interlocked.Add(ref failed, measurement);
            }
        });
        listener.Start();

        var client = factory.CreateClient();

        var user = (await (await client.PostAsJsonAsync("/api/v1/users",
                new { fullName = "Payout Metric", email = $"{Guid.NewGuid():N}@metric.example.com" }))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;
        var stokvel = (await (await client.PostAsJsonAsync("/api/v1/stokvels",
                new { name = "Metric Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;
        var add = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvel.Id}/members", new { userId = user.Id });
        Assert.True(add.IsSuccessStatusCode, "AddMember: " + (int)add.StatusCode);
        var cycle = (await (await client.PostAsJsonAsync("/api/v1/contribution-cycles",
                new { stokvelId = stokvel.Id, label = "2026-10", targetAmount = 100m }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

        var url = $"/api/v1/stokvels/{stokvel.Id}/cycles/{cycle.Id}/payout";

        var first = await client.PostAsync(url, null);
        Assert.True(first.IsSuccessStatusCode, "Payout: " + (int)first.StatusCode + " " + await first.Content.ReadAsStringAsync());
        Assert.True(Interlocked.Read(ref success) >= 1, "expected a payouts.processed measurement with outcome=success");

        // The cycle is now processed, so a second attempt is a 409 and counts as failed.
        var second = await client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.True(Interlocked.Read(ref failed) >= 1, "expected a payouts.processed measurement with outcome=failed");
    }
}