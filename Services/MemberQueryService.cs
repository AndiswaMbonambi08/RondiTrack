using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Paging;
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Services;

public class MemberQueryService(RondiTrackDbContext db)
{
    public async Task<PagedResult<StokvelMember>> ListAsync(Guid stokvelId, MemberListQuery q, CancellationToken ct)
    {
        var size = PageTokenCodec.ClampSize(q.PageSize);
        var desc = q.OrderBy?.Trim().ToLowerInvariant() switch
        {
            null or "" or "joinedatutc" or "joinedatutc asc" => false,
            "joinedatutc desc" => true,
            _ => throw new InvalidPagingException("orderBy must be 'joinedAtUtc', optionally followed by 'asc' or 'desc'.")
        };
        var hash = PageTokenCodec.Hash($"{stokvelId}|{desc}|{q.Role}");

        IQueryable<StokvelMember> query = db.StokvelMembers.AsNoTracking().Where(m => m.StokvelId == stokvelId);
        if (q.Role is { } r) query = query.Where(m => m.Role == (StokvelMemberRole)r);

        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var tok = PageTokenCodec.Decode(q.PageToken);
            if (tok.QueryHash != hash)
                throw new InvalidPagingException("pageToken does not match the current filter or sort.");
            var k = DateTime.Parse(tok.Key, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            query = desc
                ? query.Where(m => m.JoinedAtUtc < k || (m.JoinedAtUtc == k && m.UserId.CompareTo(tok.Id) > 0))
                : query.Where(m => m.JoinedAtUtc > k || (m.JoinedAtUtc == k && m.UserId.CompareTo(tok.Id) > 0));
        }

        query = desc ? query.OrderByDescending(m => m.JoinedAtUtc).ThenBy(m => m.UserId)
                     : query.OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.UserId);

        var rows = await query.Take(size + 1).ToListAsync(ct);
        var page = rows.Take(size).ToList();
        var next = rows.Count > size
            ? PageTokenCodec.Encode(new PageToken(hash, page[^1].JoinedAtUtc.ToString("O"), page[^1].UserId))
            : "";
        return new PagedResult<StokvelMember>(page, next);
    }
}