using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Paging;

namespace RondiTrack.Services;

public class ContributionQueryService(RondiTrackDbContext db)
{
    public async Task<PagedResult<Contribution>> ListAsync(
        Guid stokvelId, Guid cycleId, ContributionListQuery q, CancellationToken ct)
    {
        var size = PageTokenCodec.ClampSize(q.PageSize);
        var (field, desc) = ParseOrder(q.OrderBy);
        var hash = PageTokenCodec.Hash(
            $"{stokvelId}|{cycleId}|{field}|{desc}|{q.UserId}|{q.RecordedFrom:O}|{q.RecordedTo:O}");

        IQueryable<Contribution> query = db.Contributions.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId && c.ContributionCycleId == cycleId);
        if (q.UserId is { } u) query = query.Where(c => c.UserId == u);
        if (q.RecordedFrom is { } f) query = query.Where(c => c.RecordedAt >= f);
        if (q.RecordedTo is { } t) query = query.Where(c => c.RecordedAt <= t);

        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var tok = PageTokenCodec.Decode(q.PageToken);
            if (tok.QueryHash != hash)
                throw new InvalidPagingException("pageToken does not match the current filter or sort.");
            query = ApplyAfter(query, field, desc, tok);
        }

        // Id is the unique tiebreaker, always ascending, so equal sort values can never swap across a page boundary.
        query = (field, desc) switch
        {
            ("recordedat", false) => query.OrderBy(c => c.RecordedAt).ThenBy(c => c.Id),
            ("recordedat", true)  => query.OrderByDescending(c => c.RecordedAt).ThenBy(c => c.Id),
            ("amount", false)     => query.OrderBy(c => c.Amount).ThenBy(c => c.Id),
            _                     => query.OrderByDescending(c => c.Amount).ThenBy(c => c.Id),
        };

        var rows = await query.Take(size + 1).ToListAsync(ct);   // one extra row answers "is there more?" without COUNT(*)
        var page = rows.Take(size).ToList();
        var next = rows.Count > size ? PageTokenCodec.Encode(MakeToken(page[^1], field, hash)) : "";
        return new PagedResult<Contribution>(page, next);
    }

    static (string field, bool desc) ParseOrder(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return ("recordedat", false);
        var p = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var dir = p.Length == 2 ? p[1].ToLowerInvariant() : "asc";
        var field = p[0].ToLowerInvariant();
        if (p.Length > 2 || (dir != "asc" && dir != "desc") || (field != "recordedat" && field != "amount"))
            throw new InvalidPagingException("orderBy must be 'recordedAt' or 'amount', optionally followed by 'asc' or 'desc'.");
        return (field, dir == "desc");
    }

    static PageToken MakeToken(Contribution c, string field, string hash) =>
        new(hash, field == "recordedat" ? c.RecordedAt.ToString("O") : c.Amount.ToString(CultureInfo.InvariantCulture), c.Id);

    static IQueryable<Contribution> ApplyAfter(IQueryable<Contribution> q, string field, bool desc, PageToken t)
    {
        if (field == "recordedat")
        {
            var k = DateTime.Parse(t.Key, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return desc
                ? q.Where(c => c.RecordedAt < k || (c.RecordedAt == k && c.Id.CompareTo(t.Id) > 0))
                : q.Where(c => c.RecordedAt > k || (c.RecordedAt == k && c.Id.CompareTo(t.Id) > 0));
        }
        var a = decimal.Parse(t.Key, CultureInfo.InvariantCulture);
        return desc
            ? q.Where(c => c.Amount < a || (c.Amount == a && c.Id.CompareTo(t.Id) > 0))
            : q.Where(c => c.Amount > a || (c.Amount == a && c.Id.CompareTo(t.Id) > 0));
    }
}