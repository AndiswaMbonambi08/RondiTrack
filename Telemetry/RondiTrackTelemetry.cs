using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RondiTrack.Telemetry;

public enum PayoutOutcome { Success, Failed } //can only pass Success or Failed
public enum RefusalResult { NotFound, Forbidden }

public static class RondiTrackTelemetry
{
    public const string ActivitySourceName = "RondiTrack.Payouts";
    public const string MeterName = "RondiTrack";

    public static readonly ActivitySource Source = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName); //created once per app, not per request/static

    private static readonly Counter<long> Contributions =
        Meter.CreateCounter<long>("ronditrack.contributions.recorded");  //(ass asked 3 counters)ass required
    private static readonly Counter<long> Payouts =
        Meter.CreateCounter<long>("ronditrack.payouts.processed");  //ass required
    private static readonly Counter<long> Refusals =
        Meter.CreateCounter<long>("ronditrack.authorization.refusals"); //ass required

    public static void ContributionRecorded() => Contributions.Add(1); //has no tag, stokvel or user ID would be unbounded and personal

    public static void PayoutProcessed(PayoutOutcome outcome) =>
        Payouts.Add(1, new KeyValuePair<string, object?>("outcome",
            outcome == PayoutOutcome.Success ? "success" : "failed"));

    public static void AuthorizationRefused(RefusalResult result) =>
        Refusals.Add(1, new KeyValuePair<string, object?>("result",
            result == RefusalResult.NotFound ? "not_found" : "forbidden"));
}