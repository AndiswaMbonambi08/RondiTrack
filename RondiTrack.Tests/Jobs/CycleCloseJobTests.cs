using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Jobs;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Jobs;

[Collection("Jobs collection")]
public class CycleCloseJobTests : JobTestBase
{
    public CycleCloseJobTests(JobsApiFactory factory) : base(factory) { }

    // A cycle ending on the 25th of October.
    private static readonly DateOnly Start = new(2026, 9, 26);
    private static readonly DateOnly End = new(2026, 10, 25);

    [Fact]
    public async Task EndedCycle_IsClosed_UnderpayersGetArrearsInCents_NextCycleOpened()
    {
        // Contribution is R500: member 0 paid in full, member 1 paid R200, member 2 paid nothing.
        var s = await SeedStokvelAsync(500m, Start, End, 500m, 200m, 0m);
        var now = At(2026, 10, 26, 10, 0);

        var result = await NewCycleJob(new FakeClock(now)).RunOnceAsync();

        Assert.Equal(1, result.Closed);
        Assert.Equal(2, result.ArrearsRecorded);
        Assert.Equal(0, result.Failed);

        await WithDbAsync(async db =>
        {
            var cycle = await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == s.CycleId);
            Assert.Equal(now.UtcDateTime, cycle.ClosedAtUtc);

            var arrears = await db.Arrears.AsNoTracking()
                .Where(a => a.ContributionCycleId == s.CycleId)
                .ToDictionaryAsync(a => a.UserId, a => a.ShortfallCents);
            Assert.False(arrears.ContainsKey(s.MemberIds[0]));   // paid in full: no arrears
            Assert.Equal(30_000L, arrears[s.MemberIds[1]]);       // R300 short
            Assert.Equal(50_000L, arrears[s.MemberIds[2]]);       // R500 short

            var next = await db.ContributionCycles.AsNoTracking()
                .SingleAsync(c => c.StokvelId == s.StokvelId && c.Id != s.CycleId);
            Assert.Equal(new DateOnly(2026, 10, 26), next.StartDate);
            Assert.Equal(new DateOnly(2026, 11, 25), next.EndDate);
            Assert.Null(next.ClosedAtUtc);
            return 0;
        });
    }

    [Fact]
    public async Task CycleEndingTomorrow_IsNotTouched()
    {
        var s = await SeedStokvelAsync(500m, Start, End, 0m);

        var result = await NewCycleJob(new FakeClock(At(2026, 10, 24, 12, 0))).RunOnceAsync();

        Assert.Equal(0, result.Closed);
        await WithDbAsync(async db =>
        {
            Assert.Null((await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == s.CycleId)).ClosedAtUtc);
            Assert.Equal(1, await db.ContributionCycles.CountAsync(c => c.StokvelId == s.StokvelId));
            Assert.Equal(0, await db.Arrears.CountAsync());
            return 0;
        });
    }

    [Fact]
    public async Task At2359Sast_OnTheEndDate_CycleStaysOpen()
    {
        var s = await SeedStokvelAsync(500m, Start, End, 0m);

        var result = await NewCycleJob(new FakeClock(At(2026, 10, 25, 23, 59))).RunOnceAsync();

        Assert.Equal(0, result.Closed);
        await WithDbAsync(async db =>
        {
            Assert.Null((await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == s.CycleId)).ClosedAtUtc);
            return 0;
        });
    }

    [Fact]
    public async Task At0000Sast_TheNextDay_CycleCloses()
    {
        var s = await SeedStokvelAsync(500m, Start, End, 0m);

        var result = await NewCycleJob(new FakeClock(At(2026, 10, 26, 0, 0))).RunOnceAsync();

        Assert.Equal(1, result.Closed);
        await WithDbAsync(async db =>
        {
            // 00:00 SAST on the 26th is 22:00 UTC on the 25th.
            var cycle = await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == s.CycleId);
            Assert.Equal(new DateTime(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc), cycle.ClosedAtUtc);
            return 0;
        });
    }

    [Fact]
    public void CloseInstant_ForACycleEndingOnThe25th_Is2200UtcOnThe25th()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        var cycle = new ContributionCycle(Guid.NewGuid(), "2026-10", 500m, Start, End);

        Assert.Equal(new DateTime(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc), cycle.CloseInstantUtc(zone));
    }

    [Fact]
    public async Task RunningTwice_GivesExactlyTheSameStateAsRunningOnce()
    {
        await SeedStokvelAsync(500m, Start, End, 500m, 200m, 0m);
        var job = NewCycleJob(new FakeClock(At(2026, 10, 26, 10, 0)));

        await job.RunOnceAsync();
        var afterFirst = await StateAsync();
        var second = await job.RunOnceAsync();
        var afterSecond = await StateAsync();

        Assert.Equal(0, second.Closed);
        Assert.Equal(0, second.ArrearsRecorded);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task TwoConcurrentRuns_CloseEachCycleOnce_AndRecordEachArrearOnce()
    {
        var stokvels = new List<Seeded>();
        for (var i = 0; i < 5; i++)
            stokvels.Add(await SeedStokvelAsync(500m, Start, End, 0m, 0m));
        var clock = new FakeClock(At(2026, 10, 26, 10, 0));

        // Two runs started together, each with its own scopes and DbContexts.
        var results = await Task.WhenAll(
            NewCycleJob(clock).RunOnceAsync(),
            NewCycleJob(clock).RunOnceAsync());

        Assert.Equal(5, results.Sum(r => r.Closed));
        Assert.Equal(0, results.Sum(r => r.Failed));

        await WithDbAsync(async db =>
        {
            foreach (var s in stokvels)
            {
                Assert.Equal(1, await db.ContributionCycles.CountAsync(c => c.StokvelId == s.StokvelId && c.ClosedAtUtc != null));
                Assert.Equal(2, await db.ContributionCycles.CountAsync(c => c.StokvelId == s.StokvelId)); // old + one next
                Assert.Equal(2, await db.Arrears.CountAsync(a => a.StokvelId == s.StokvelId));             // one per member
            }
            return 0;
        });
    }

    [Fact]
    public async Task ThreeCyclesBehind_ClosesTheOverdueOne_OpensOnlyTheCurrentPeriod_SecondRunChangesNothing()
    {
        // The API was down since the 25 July cycle ended: August, September and October were all missed.
        var s = await SeedStokvelAsync(500m, new DateOnly(2026, 6, 26), new DateOnly(2026, 7, 25), 0m);
        var job = NewCycleJob(new FakeClock(At(2026, 10, 26, 10, 0)));

        var first = await job.RunOnceAsync();
        Assert.Equal(1, first.Closed);

        await WithDbAsync(async db =>
        {
            var cycles = await db.ContributionCycles.AsNoTracking()
                .Where(c => c.StokvelId == s.StokvelId)
                .OrderBy(c => c.StartDate)
                .ToListAsync();
            Assert.Equal(2, cycles.Count);                                   // overdue + ONE new, not several
            Assert.NotNull(cycles[0].ClosedAtUtc);
            Assert.Equal(new DateOnly(2026, 10, 26), cycles[1].StartDate);   // the period containing today
            Assert.Equal(new DateOnly(2026, 11, 25), cycles[1].EndDate);
            Assert.Null(cycles[1].ClosedAtUtc);
            // The overdue cycle was open, so its member still gets arrears.
            Assert.Equal(1, await db.Arrears.CountAsync(a => a.ContributionCycleId == s.CycleId));
            return 0;
        });

        var afterFirst = await StateAsync();
        var second = await job.RunOnceAsync();
        Assert.Equal(0, second.Closed);
        Assert.Equal(afterFirst, await StateAsync());
    }

    [Fact]
    public async Task OneBrokenStokvel_DoesNotStopAnotherFromClosing_AndTheRunDoesNotThrow()
    {
        // Broken: an arrear already exists for a cycle that is still open, which is an impossible state.
        // The job's own arrears insert then hits ux_arrears_cycle_user, and that stokvel must roll back.
        var broken = await SeedStokvelAsync(500m, new DateOnly(2026, 9, 21), new DateOnly(2026, 10, 20), 0m);
        await WithDbAsync(async db =>
        {
            db.Arrears.Add(new Arrear(broken.CycleId, broken.StokvelId, broken.MemberIds[0], 100, DateTime.UtcNow));
            await db.SaveChangesAsync();
            return 0;
        });
        var good = await SeedStokvelAsync(500m, Start, End, 0m);   // ends later, so it is processed second

        CycleCloseRunResult? result = null;
        var thrown = await Record.ExceptionAsync(async () =>
            result = await NewCycleJob(new FakeClock(At(2026, 10, 26, 10, 0))).RunOnceAsync());

        Assert.Null(thrown);
        Assert.Equal(1, result!.Closed);
        Assert.Equal(1, result.Failed);

        await WithDbAsync(async db =>
        {
            Assert.NotNull((await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == good.CycleId)).ClosedAtUtc);

            // The broken stokvel is untouched: still open, no new cycle, only the arrear we inserted.
            Assert.Null((await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == broken.CycleId)).ClosedAtUtc);
            Assert.Equal(1, await db.ContributionCycles.CountAsync(c => c.StokvelId == broken.StokvelId));
            Assert.Equal(1, await db.Arrears.CountAsync(a => a.ContributionCycleId == broken.CycleId));
            return 0;
        });
    }
}