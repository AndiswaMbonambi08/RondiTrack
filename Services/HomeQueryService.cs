using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Jobs;

namespace RondiTrack.Services;

// Builds the home screen with a fixed number of queries, however many stokvels the user belongs to.
public sealed class HomeQueryService(
    RondiTrackDbContext db, TimeProvider time, IOptions<CycleCloseOptions> cycleOptions)
{
    public async Task<HomeResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var zone = cycleOptions.Value.ResolveTimeZone();

        var memberships = await (
            from m in db.StokvelMembers.AsNoTracking()
            join s in db.Stokvels.AsNoTracking() on m.StokvelId equals s.Id
            where m.UserId == userId
            orderby s.Name
            select new { s.Id, s.Name, s.ContributionAmount, m.Role }
        ).ToListAsync(ct);

        if (memberships.Count == 0)
            return new HomeResponse(now, []);

        var stokvelIds = memberships.Select(m => m.Id).ToList();

        // "Current cycle" = the newest cycle that is not PayoutProcessed. Cycles carry no created date,
        // so newest means the highest label ("2026-10" sorts after "2026-09").
        var openCycles = await db.ContributionCycles.AsNoTracking()
            .Where(c => stokvelIds.Contains(c.StokvelId) && c.Status != CycleStatus.PayoutProcessed)
            .Select(c => new { c.Id, c.StokvelId, c.Label, c.EndDate, c.ClosedAtUtc })
            .ToListAsync(ct);

        var currentByStokvel = openCycles
            .GroupBy(c => c.StokvelId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Label, StringComparer.Ordinal).First());

        var cycleIds = currentByStokvel.Values.Select(c => c.Id).ToList();

        // Only the caller's own contributions are read.
        var paidByCycle = (await db.Contributions.AsNoTracking()
                .Where(c => c.UserId == userId && cycleIds.Contains(c.ContributionCycleId))
                .Select(c => new { c.ContributionCycleId, c.Amount })
                .ToListAsync(ct))
            .GroupBy(c => c.ContributionCycleId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        var items = memberships.Select(m =>
        {
            if (!currentByStokvel.TryGetValue(m.Id, out var cycle))
                return new HomeStokvelResponse(m.Id, m.Name, m.Role.ToString(), null, 0, 0, null);

            var paid = paidByCycle.GetValueOrDefault(cycle.Id);
            var owed = Math.Max(0m, m.ContributionAmount - paid);

            return new HomeStokvelResponse(
                m.Id, m.Name, m.Role.ToString(),
                new HomeCycleResponse(cycle.Id, cycle.Label, cycle.ClosedAtUtc is null ? "Open" : "Closed", cycle.EndDate),
                ToCents(paid), ToCents(owed),
                NextPayoutFromLabel(cycle.Label, zone));
        }).ToList();

        return new HomeResponse(now, items);
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);

    // "2026-10" -> the last moment of October in the stokvel's time zone, as a UTC instant
    // (23:59:59 in Johannesburg = 21:59:59Z). Any label that is not yyyy-MM gives null.
    internal static DateTime? NextPayoutFromLabel(string label, TimeZoneInfo zone)
    {
        if (!DateTime.TryParseExact(label, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            return null;

        var localLastMoment = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month),
            23, 59, 59, DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(localLastMoment, zone);
    }
}