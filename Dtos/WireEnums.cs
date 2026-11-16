using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace RondiTrack.Dtos;

// Publishes role and cycle status as enums in the OpenAPI document.
// The wire format is unchanged: the API still sends these exact strings.
internal static class WireEnums
{
    private static readonly string[] Roles = ["Member", "Treasurer", "Admin"];
    private static readonly string[] CycleStatuses = ["Open", "Closed", "PaidOut"];

    public static void Apply(OpenApiSchema schema, Type type)
    {
        if (type == typeof(HomeStokvelResponse) || type == typeof(StokvelDetailResponse))
            Set(schema, "role", Roles);

        if (type == typeof(HomeCycleResponse) || type == typeof(CycleSummaryResponse) || type == typeof(StokvelCycleSummary))
            Set(schema, "status", CycleStatuses);
    }

    private static void Set(OpenApiSchema schema, string name, string[] values)
    {
        if (schema.Properties is not null && schema.Properties.TryGetValue(name, out var p) && p is OpenApiSchema s)
            s.Enum = values.Select(v => (JsonNode)JsonValue.Create(v)!).ToList();
    }
}