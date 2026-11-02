using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Paging;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Services;

// Recent activity across every stokvel the caller belongs to, newest first.
// Three sources (contributions, payouts, members joining) are each read with the same cursor,
// merged, and cut to one page. Each source returns at most size + 1 rows, so the merged top
// size + 1 is exactly right.
public static class ActivityFeedQuery
{
    private sealed record Event(Guid Id, string Kind, Guid StokvelId, DateTime At, Guid? ActorId, long? Cents);

    public static async Task<PagedResult<ActivityItemResponse>> ListAsync(
        RondiTrackDbContext db, Guid me, ActivityQuery q, CancellationToken ct)
    {
        var size = PageTokenCodec.ClampSize(q.PageSize);
        var hash = PageTokenCodec.Hash($"{me}|activity");

        var hasCursor = false;
        var key = default(DateTime);
        var cursorId = Guid.Empty;
        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var tok = PageTokenCodec.Decode(q.PageToken);
            if (tok.QueryHash != hash)
                throw new InvalidPagingException("pageToken does not match this list.");
            if (!DateTime.TryParse(tok.Key, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out key))
                throw new InvalidPagingException("Invalid pageToken.");
            cursorId = tok.Id;
            hasCursor = true;
        }

        var memberships = await (
            from m in db.StokvelMembers.AsNoTracking()
            join s in db.Stokvels.AsNoTracking() on m.StokvelId equals s.Id
            where m.UserId == me
            select new { s.Id, s.Name, m.Role }
        ).ToListAsync(ct);

        if (memberships.Count == 0)
            return new PagedResult<ActivityItemResponse>([], "");

        var allIds = memberships.Select(m => m.Id).ToList();
        // Treasurers and Admins see everyone's contributions and names in their stokvels.
        var privIds = memberships.Where(m => m.Role >= StokvelMemberRole.Treasurer).Select(m => m.Id).ToList();
        var stokvelNames = memberships.ToDictionary(m => m.Id, m => m.Name);
        var take = size + 1;

        var contribs = db.Contributions.AsNoTracking()
            .Where(c => allIds.Contains(c.StokvelId) && (privIds.Contains(c.StokvelId) || c.UserId == me));
        var payouts = db.Payouts.AsNoTracking().Where(p => allIds.Contains(p.StokvelId));
        var joins = db.StokvelMembers.AsNoTracking().Where(m => allIds.Contains(m.StokvelId));

        if (hasCursor)
        {
            var k = key;
            var id = cursorId;
            contribs = contribs.Where(c => c.RecordedAt < k || (c.RecordedAt == k && c.Id.CompareTo(id) > 0));
            payouts = payouts.Where(p => p.PayoutDate < k || (p.PayoutDate == k && p.Id.CompareTo(id) > 0));
            joins = joins.Where(m => m.JoinedAtUtc < k || (m.JoinedAtUtc == k && m.UserId.CompareTo(id) > 0));
        }

        var cRows = await contribs.OrderByDescending(c => c.RecordedAt).ThenBy(c => c.Id).Take(take)
            .Select(c => new { c.Id, c.StokvelId, c.UserId, c.Amount, c.RecordedAt }).ToListAsync(ct);
        var pRows = await payouts.OrderByDescending(p => p.PayoutDate).ThenBy(p => p.Id).Take(take)
            .Select(p => new { p.Id, p.StokvelId, p.RecipientUserId, p.Amount, p.PayoutDate }).ToListAsync(ct);
        var jRows = await joins.OrderByDescending(m => m.JoinedAtUtc).ThenBy(m => m.UserId).Take(take)
            .Select(m => new { m.StokvelId, m.UserId, m.JoinedAtUtc }).ToListAsync(ct);

        var events = new List<Event>();
        events.AddRange(cRows.Select(c => new Event(c.Id, "Contribution", c.StokvelId, c.RecordedAt, c.UserId, ToCents(c.Amount))));
        events.AddRange(pRows.Select(p => new Event(p.Id, "Payout", p.StokvelId, p.PayoutDate, p.RecipientUserId, ToCents(p.Amount))));
        // A membership has no id of its own, so the member's user id is its tiebreaker.
        events.AddRange(jRows.Select(j => new Event(j.UserId, "MemberJoined", j.StokvelId, j.JoinedAtUtc, j.UserId, null)));

        // "N" format sorts the same way PostgreSQL sorts a uuid, so ties break exactly as the database does.
        var ordered = events
            .OrderByDescending(e => e.At)
            .ThenBy(e => e.Id.ToString("N"), StringComparer.Ordinal)
            .ToList();
        var page = ordered.Take(size).ToList();
        var next = ordered.Count > size
            ? PageTokenCodec.Encode(new PageToken(hash, page[^1].At.ToString("O"), page[^1].Id))
            : "";

        var actorIds = page.Where(e => e.ActorId is not null).Select(e => e.ActorId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var items = page.Select(e =>
        {
            string? actor = null;
            if (e.ActorId is { } a && (privIds.Contains(e.StokvelId) || a == me))
                actor = names.GetValueOrDefault(a);
            return new ActivityItemResponse(e.Id, e.Kind, e.StokvelId, stokvelNames[e.StokvelId], e.At, actor, e.Cents);
        }).ToList();

        return new PagedResult<ActivityItemResponse>(items, next);
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);
}