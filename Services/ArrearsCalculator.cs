using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;

namespace RondiTrack.Services;

public sealed record Shortfall(Guid UserId, long Cents);

// The one place that decides who owes what when a cycle closes. The cycle-close job and the
// close preview both call it, so the dialog can never disagree with the real close.
public static class ArrearsCalculator
{
    public static async Task<List<Shortfall>> ComputeAsync(
        RondiTrackDbContext db, Guid cycleId, Guid stokvelId, CancellationToken ct)
    {
        var contributionAmount = await db.Stokvels.AsNoTracking()
            .Where(s => s.Id == stokvelId)
            .Select(s => (decimal?)s.ContributionAmount)
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The cycle's stokvel does not exist.");

        var memberIds = await db.StokvelMembers.AsNoTracking()
            .Where(sm => sm.StokvelId == stokvelId)
            .Select(sm => sm.UserId)
            .ToListAsync(ct);

        var paid = await db.Contributions.AsNoTracking()
            .Where(c => c.ContributionCycleId == cycleId)
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.UserId, x => x.Total, ct);

        var result = new List<Shortfall>();
        foreach (var memberId in memberIds)
        {
            var shortfall = contributionAmount - paid.GetValueOrDefault(memberId);
            var cents = (long)Math.Round(shortfall * 100m, 0, MidpointRounding.AwayFromZero);
            if (cents > 0) result.Add(new Shortfall(memberId, cents));
        }
        return result;
    }
}