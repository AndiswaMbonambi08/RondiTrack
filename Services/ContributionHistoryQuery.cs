using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Paging;

namespace RondiTrack.Services;

// A stokvel's contributions, filtered, newest first (or oldest first), with keyset paging.
// The time range is a half-open range of UTC instants: [From, To).
public static partial class ContributionHistoryQuery
{
    public sealed record Parsed(Guid? Member, DateTime? From, DateTime? To, bool OldestFirst);

    // "2026-09-30T22:00:00Z" or "2026-10-01T00:00:00+02:00". A date with no time, or an instant
    // with no zone, is refused: that is exactly how a date gets read as midnight UTC by mistake.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex InstantShape();

    private static DateTime? Instant(string? raw, string name)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        if (!InstantShape().IsMatch(text) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            throw new InvalidPagingException($"{name} must be a UTC instant such as 2026-09-30T22:00:00Z.");
        }
        return value.UtcDateTime;
    }

    public static Parsed Parse(HistoryQuery q)
    {
        Guid? member = null;
        if (!string.IsNullOrWhiteSpace(q.Member))
        {
            if (!Guid.TryParse(q.Member, out var id))
                throw new InvalidPagingException("member must be a member id.");
            member = id;
        }

        var from = Instant(q.From, "from");
        var to = Instant(q.To, "to");
        if (from is not null && to is not null && to <= from)
            throw new InvalidPagingException("to must be later than from.");

        var oldest = q.Sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "newest" => false,
            "oldest" => true,
            _ => throw new InvalidPagingException("sort must be 'newest' or 'oldest'.")
        };

        return new Parsed(member, from, to, oldest);
    }

    public static async Task<PagedResult<HistoryItemResponse>> ListAsync(
        RondiTrackDbContext db, Guid stokvelId, Guid? member, Parsed p, HistoryQuery q, CancellationToken ct)
    {
        var size = PageTokenCodec.ClampSize(q.PageSize);
        // The token is only good for THIS list: same stokvel, member, range and sort.
        var hash = PageTokenCodec.Hash($"{stokvelId}|{member}|{p.From:O}|{p.To:O}|{p.OldestFirst}");

        var mine = db.Contributions.AsNoTracking().Where(c => c.StokvelId == stokvelId);
        if (member is { } m) mine = mine.Where(c => c.UserId == m);
        if (p.From is { } f) mine = mine.Where(c => c.RecordedAt >= f);   // included
        if (p.To is { } t) mine = mine.Where(c => c.RecordedAt < t);       // excluded

        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var tok = PageTokenCodec.Decode(q.PageToken);
            if (tok.QueryHash != hash)
                throw new InvalidPagingException("pageToken does not match the current filter or sort.");
            if (!DateTime.TryParse(tok.Key, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var key))
                throw new InvalidPagingException("Invalid pageToken.");
            var id = tok.Id;
            mine = p.OldestFirst
                ? mine.Where(c => c.RecordedAt > key || (c.RecordedAt == key && c.Id.CompareTo(id) > 0))
                : mine.Where(c => c.RecordedAt < key || (c.RecordedAt == key && c.Id.CompareTo(id) > 0));
        }

        var joined =
            from c in mine
            join u in db.Users.AsNoTracking() on c.UserId equals u.Id
            join cy in db.ContributionCycles.AsNoTracking() on c.ContributionCycleId equals cy.Id
            select new { c.Id, c.UserId, u.FullName, c.Amount, c.Status, c.RecordedAt, CycleId = cy.Id, CycleLabel = cy.Label, HasProof = db.ProofsOfPayment.Any(p => p.ContributionId == c.Id), CycleClosed = cy.ClosedAtUtc != null };

        // Id is the unique tiebreaker, always ascending.
        var ordered = p.OldestFirst
            ? joined.OrderBy(r => r.RecordedAt).ThenBy(r => r.Id)
            : joined.OrderByDescending(r => r.RecordedAt).ThenBy(r => r.Id);

        var rows = await ordered.Take(size + 1).ToListAsync(ct);
        var page = rows.Take(size).ToList();
        var next = rows.Count > size
            ? PageTokenCodec.Encode(new PageToken(hash, page[^1].RecordedAt.ToString("O"), page[^1].Id))
            : "";

        var items = page.Select(r => new HistoryItemResponse(
            r.Id, r.UserId, r.FullName, ToCents(r.Amount), r.Status, r.RecordedAt, r.CycleId, r.CycleLabel, r.HasProof, r.CycleClosed)).ToList();
        return new PagedResult<HistoryItemResponse>(items, next);
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);
}