using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Jobs;
using RondiTrack.Persistence.Entities;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests.Jobs;

// Every job test starts from empty tables, so counts are exact.
public abstract class JobTestBase : IAsyncLifetime
{
    protected readonly JobsApiFactory Factory;

    protected JobTestBase(JobsApiFactory factory) => Factory = factory;

    // South Africa is UTC+2 all year (no daylight saving).
    protected static DateTimeOffset At(int y, int m, int d, int h, int min) =>
        new(y, m, d, h, min, 0, TimeSpan.FromHours(2));

    public async Task InitializeAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Arrears\",\"Contributions\",\"Payouts\",\"ContributionCycles\"," +
            "\"StokvelMembers\",\"Stokvels\",\"RefreshTokens\",\"IdempotencyEntries\",\"Users\" CASCADE");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected CycleCloseJob NewCycleJob(TimeProvider time) => new(
        Factory.Services.GetRequiredService<IServiceScopeFactory>(),
        time,
        Options.Create(new CycleCloseOptions()),
        NullLogger<CycleCloseJob>.Instance);

    protected HousekeepingJob NewHousekeepingJob(TimeProvider time, int tokenDays = 30, int idempotencyHours = 168) => new(
        Factory.Services.GetRequiredService<IServiceScopeFactory>(),
        time,
        Options.Create(new HousekeepingOptions
        {
            RefreshTokenRetentionDays = tokenDays,
            IdempotencyRetentionHours = idempotencyHours
        }),
        NullLogger<HousekeepingJob>.Instance);

    protected async Task<T> WithDbAsync<T>(Func<RondiTrackDbContext, Task<T>> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>());
    }

    protected sealed record Seeded(Guid StokvelId, Guid CycleId, Guid[] MemberIds);

    // One stokvel with one scheduled cycle. paidAmounts has one entry per member: 0 means "paid nothing".
    protected async Task<Seeded> SeedStokvelAsync(
        decimal contribution, DateOnly start, DateOnly end, params decimal[] paidAmounts)
    {
        return await WithDbAsync(async db =>
        {
            var stokvel = new Stokvel($"Job Test {Guid.NewGuid():N}", contribution);
            var users = paidAmounts
                .Select((_, i) => new User($"Member {i}", $"{Guid.NewGuid():N}@test.example"))
                .ToList();
            var cycle = new ContributionCycle(stokvel.Id, CycleCalendar.Label(end), contribution, start, end);

            db.Users.AddRange(users);
            db.Stokvels.Add(stokvel);
            db.StokvelMembers.AddRange(users.Select(u =>
                new StokvelMember(stokvel.Id, u.Id, StokvelMemberRole.Member, DateTime.UtcNow)));
            db.ContributionCycles.Add(cycle);
            await db.SaveChangesAsync();

            // Raw SQL: this inserts exactly what a recorded contribution leaves in the table.
            for (var i = 0; i < users.Count; i++)
            {
                if (paidAmounts[i] <= 0) continue;
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO \"Contributions\" (\"Id\",\"Amount\",\"ContributionCycleId\",\"RecordedAt\",\"StokvelId\",\"UserId\") VALUES ({Guid.NewGuid()}, {paidAmounts[i]}, {cycle.Id}, {DateTime.UtcNow}, {stokvel.Id}, {users[i].Id})");
            }

            return new Seeded(stokvel.Id, cycle.Id, users.Select(u => u.Id).ToArray());
        });
    }

    // Everything the cycle-close job can change, as text, so two states can be compared exactly.
    protected Task<string> StateAsync() => WithDbAsync(async db =>
    {
        var cycles = await db.ContributionCycles.AsNoTracking()
            .OrderBy(c => c.StokvelId).ThenBy(c => c.StartDate)
            .Select(c => new { c.Id, c.StartDate, c.EndDate, c.ClosedAtUtc })
            .ToListAsync();
        var arrears = await db.Arrears.AsNoTracking()
            .OrderBy(a => a.ContributionCycleId).ThenBy(a => a.UserId)
            .Select(a => new { a.ContributionCycleId, a.UserId, a.ShortfallCents })
            .ToListAsync();

        return string.Join("\n", cycles.Select(c => $"C {c.Id} {c.StartDate} {c.EndDate} {c.ClosedAtUtc:O}"))
             + "\n"
             + string.Join("\n", arrears.Select(a => $"A {a.ContributionCycleId} {a.UserId} {a.ShortfallCents}"));
    });
}