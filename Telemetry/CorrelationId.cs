using System.Diagnostics;

namespace RondiTrack.Telemetry;

public static class CorrelationId
{
    public static string For(HttpContext ctx) =>
        Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;
}