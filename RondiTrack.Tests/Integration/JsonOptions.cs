// Shared case-insensitive JSON options for deserializing API responses in tests,
// since the API returns camelCase property names.
using System.Text.Json;

namespace RondiTrack.Tests.Integration;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };
}