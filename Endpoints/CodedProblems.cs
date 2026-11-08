using System.Diagnostics;

namespace RondiTrack.Endpoints;

// A problem+json answer with a machine-readable "code", so the web app can pick a screen
// without reading the English text.
internal static class CodedProblems
{
    public static IResult Make(HttpContext http, int status, string title, string detail, string code,
                               Dictionary<string, object?>? extra = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier
        };
        if (extra is not null) foreach (var kv in extra) extensions[kv.Key] = kv.Value;
        return Results.Problem(title: title, detail: detail, statusCode: status, extensions: extensions);
    }
}