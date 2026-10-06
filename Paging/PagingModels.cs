using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Paging;

public sealed class InvalidPagingException(string message) : RequestValidationException(message);

public record ContributionListQuery(
    int? PageSize, string? PageToken, string? OrderBy,
    Guid? UserId, DateTime? RecordedFrom, DateTime? RecordedTo);

public record MemberListQuery(int? PageSize, string? PageToken, string? OrderBy, int? Role);

public record PagedResult<T>(IReadOnlyList<T> Items, string NextPageToken);   // NextPageToken == "" means no more results

public record PageToken(string QueryHash, string Key, Guid Id);

public static class PageTokenCodec
{
    public static string Encode(PageToken t) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(t))
               .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static PageToken Decode(string s)
    {
        try
        {
            var b64 = s.Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            return JsonSerializer.Deserialize<PageToken>(Convert.FromBase64String(b64))
                   ?? throw new InvalidPagingException("Invalid pageToken.");
        }
        catch (Exception e) when (e is not InvalidPagingException)
        {
            throw new InvalidPagingException("Invalid pageToken.");
        }
    }

    public static string Hash(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];

    // Optional, default 25, reduced to 100 when larger, rejected when negative.
    public static int ClampSize(int? requested) => requested switch
    {
        null or 0 => 25,
        < 0 => throw new InvalidPagingException("pageSize must not be negative."),
        > 100 => 100,
        var s => s.Value
    };
}