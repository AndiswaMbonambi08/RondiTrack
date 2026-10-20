using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Dtos;
using RondiTrack.Paging;

namespace RondiTrack.Services;

// The caller's own contributions in one stokvel, newest first, with keyset paging.
public static class MyContributionQuery
{
    public static async Task<PagedResult<ContributionItemResponse>> ListAsync(
        RondiTrackDbContext db, Guid stokvelId, Guid userId, MyContributionListQuery q, CancellationToken ct)
    {
        var size = PageTokenCodec.ClampSize(q.PageSize);
        var hash = PageTokenCodec.Hash($"{stokvelId}|{userId}|mine");

        var mine = db.Contributions.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId && c.UserId == userId);

        if (!string.IsNullOrEmpty(q.PageToken))
        {
            var tok = PageTokenCodec.Decode(q.PageToken);
            if (tok.QueryHash != hash)
                throw new InvalidPagingException("pageToken does not match the current filter or sort.");
            var key = DateTime.Parse(tok.Key, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            mine = mine.Where(c => c.RecordedAt < key || (c.RecordedAt == key && c.Id.CompareTo(tok.Id) > 0));
        }

        // Id is the unique tiebreaker, always ascending.
        var rows = await (
            from c in mine
            join cy in db.ContributionCycles.AsNoTracking() on c.ContributionCycleId equals cy.Id
            orderby c.RecordedAt descending, c.Id
            select new { c.Id, c.Amount, c.Status, c.RecordedAt, CycleId = cy.Id, CycleLabel = cy.Label }
        ).Take(size + 1).ToListAsync(ct);

        var page = rows.Take(size).ToList();
        var next = rows.Count > size
            ? PageTokenCodec.Encode(new PageToken(hash, page[^1].RecordedAt.ToString("O"), page[^1].Id))
            : "";

        var items = page.Select(r => new ContributionItemResponse(
            r.Id, ToCents(r.Amount), r.Status, r.RecordedAt, r.CycleId, r.CycleLabel)).ToList();

        return new PagedResult<ContributionItemResponse>(items, next);
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);
}