// Closes stokvel cycles that have ended, records arrears and opens the next cycle.
//
// Rules (also in the README):
//  * A cycle ending on date D closes at 00:00 on D+1 in Africa/Johannesburg (SAST, UTC+2, no DST).
//    For D = the 25th that is 22:00 UTC on the 25th. "Now" always comes from TimeProvider.
//  * Exactly once is enforced by the database: a conditional UPDATE ... WHERE ClosedAtUtc IS NULL.
//    Whoever affects 1 row owns the close; anyone else affects 0 rows and does nothing.
//  * One stokvel per transaction. A failure rolls that stokvel back and never stops the others.
//  * Catch-up: the overdue cycle is closed (it WAS open, so members could pay into it and owe arrears),
//    then exactly ONE new cycle is opened: the period that contains today. Periods that were missed
//    entirely (API down) are skipped, not invented, because nobody could have paid into a cycle that
//    never existed. The skip is logged as a warning with the number of periods.
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Telemetry;

namespace RondiTrack.Jobs;

public sealed record CycleCloseRunResult(int Closed, int AlreadyClosedByOthers, int Failed, int ArrearsRecorded);

public class CycleCloseJob : BackgroundService
{
    private sealed record CloseOutcome(bool Closed, int ArrearsRecorded, int PeriodsSkipped);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly CycleCloseOptions _options;
    private readonly TimeZoneInfo _zone;
    private readonly ILogger<CycleCloseJob> _logger;

    public CycleCloseJob(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<CycleCloseOptions> options,
        ILogger<CycleCloseJob> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _zone = _options.ResolveTimeZone();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Cycle-close job is disabled (CycleClose:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes), _time);

        // Runs once straight away (catches up after downtime), then on every tick.
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let an exception escape: that would stop the host. The next tick retries.
                _logger.LogError(ex, "Cycle-close run failed; it will be retried on the next tick.");
            }
        }
        while (await NextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> NextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    // Tests call this directly with a fake TimeProvider.
    public async Task<CycleCloseRunResult> RunOnceAsync(CancellationToken ct = default)
    {
        using var run = RondiTrackTelemetry.Source.StartActivity("cycle-close.run");

        var now = _time.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var today = CycleCalendar.Today(now, _zone);

        // A cycle is due once the stokvel's local date is AFTER its EndDate,
        // which is exactly "now >= 00:00 on EndDate + 1 in the stokvel's zone".
        List<(Guid CycleId, Guid StokvelId)> due;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            var rows = await db.ContributionCycles
                .AsNoTracking()
                .Where(c => c.ClosedAtUtc == null && c.EndDate != null && c.EndDate < today)
                .OrderBy(c => c.EndDate)
                .Select(c => new { c.Id, c.StokvelId })
                .ToListAsync(ct);
            due = rows.Select(r => (r.Id, r.StokvelId)).ToList();
        }

        int closed = 0, alreadyClosed = 0, failed = 0, arrears = 0;

        foreach (var (cycleId, stokvelId) in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var outcome = await CloseOneAsync(cycleId, stokvelId, nowUtc, today, ct);
                if (!outcome.Closed)
                {
                    alreadyClosed++;
                    continue;
                }

                closed++;
                arrears += outcome.ArrearsRecorded;

                // Counted only after the transaction committed.
                RondiTrackTelemetry.CycleClosed();
                RondiTrackTelemetry.ArrearsRecorded(outcome.ArrearsRecorded);

                _logger.LogInformation(
                    "Cycle closed. StokvelId: {StokvelId}, CycleId: {CycleId}, ArrearsRecorded: {ArrearsRecorded}",
                    stokvelId, cycleId, outcome.ArrearsRecorded);

                if (outcome.PeriodsSkipped > 0)
                    _logger.LogWarning(
                        "Stokvel was behind; missed periods were skipped, not backfilled. StokvelId: {StokvelId}, PeriodsSkipped: {PeriodsSkipped}",
                        stokvelId, outcome.PeriodsSkipped);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // That stokvel's transaction rolled back, so it is untouched and will be retried next tick.
                failed++;
                _logger.LogError(ex,
                    "Closing cycle failed. StokvelId: {StokvelId}, CycleId: {CycleId}", stokvelId, cycleId);
            }
        }

        run?.SetTag("cycles.closed", closed);
        run?.SetTag("cycles.failed", failed);
        run?.SetTag("arrears.recorded", arrears);

        return new CycleCloseRunResult(closed, alreadyClosed, failed, arrears);
    }

    private async Task<CloseOutcome> CloseOneAsync(
        Guid cycleId, Guid stokvelId, DateTime nowUtc, DateOnly today, CancellationToken ct)
    {
        using var activity = RondiTrackTelemetry.Source.StartActivity("cycle-close.stokvel");
        activity?.SetTag("stokvel.id", stokvelId);
        activity?.SetTag("cycle.id", cycleId);

        // A fresh scope (so a fresh DbContext) per stokvel: a failure here can't poison the next one.
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        // The DbContext uses EnableRetryOnFailure, so a manual transaction must run inside the execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<CloseOutcome>(async token =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(token);

            // 1. Claim the close. One conditional UPDATE: the database, not C#, decides the winner.
            //    A concurrent runner blocks on the row until we commit, then matches 0 rows.
            var claimed = await db.ContributionCycles
                .Where(c => c.Id == cycleId && c.ClosedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, nowUtc), token);

            if (claimed == 0)
            {
                await tx.RollbackAsync(token);
                return new CloseOutcome(false, 0, 0);
            }

            var cycle = await db.ContributionCycles.AsNoTracking()
                .Where(c => c.Id == cycleId)
                .Select(c => new { c.TargetAmount, c.EndDate })
                .SingleAsync(token);

            // An impossible state (e.g. the stokvel is gone) throws here, rolling back the claim above.
            var contributionAmount = await db.Stokvels.AsNoTracking()
                .Where(s => s.Id == stokvelId)
                .Select(s => (decimal?)s.ContributionAmount)
                .SingleOrDefaultAsync(token)
                ?? throw new InvalidOperationException("The cycle's stokvel does not exist.");

            // 2. Arrears: every member who paid less than the stokvel's contribution amount.
            var memberIds = await db.StokvelMembers.AsNoTracking()
                .Where(sm => sm.StokvelId == stokvelId)
                .Select(sm => sm.UserId)
                .ToListAsync(token);

            var paid = await db.Contributions.AsNoTracking()
                .Where(c => c.ContributionCycleId == cycleId)
                .GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, Total = g.Sum(c => c.Amount) })
                .ToDictionaryAsync(x => x.UserId, x => x.Total, token);

            var arrears = new List<Arrear>();
            foreach (var memberId in memberIds)
            {
                var shortfall = contributionAmount - paid.GetValueOrDefault(memberId);
                var cents = (long)Math.Round(shortfall * 100m, 0, MidpointRounding.AwayFromZero);
                if (cents > 0)
                    arrears.Add(new Arrear(cycleId, stokvelId, memberId, cents, nowUtc));
            }
            db.Arrears.AddRange(arrears);

            // 3. Open the next cycle: the period that contains today (see the catch-up rule above).
            var (start, end) = CycleCalendar.NextAfter(cycle.EndDate!.Value, _options.CycleEndDay);
            var skipped = 0;
            while (end < today)
            {
                skipped++;
                (start, end) = CycleCalendar.NextAfter(end, _options.CycleEndDay);
            }

            var alreadyExists = await db.ContributionCycles
                .AnyAsync(c => c.StokvelId == stokvelId && c.StartDate == start, token);
            if (!alreadyExists)
                db.ContributionCycles.Add(new ContributionCycle(
                    stokvelId, CycleCalendar.Label(end), cycle.TargetAmount, start, end));

            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);

            return new CloseOutcome(true, arrears.Count, skipped);
        }, ct);
    }
}
