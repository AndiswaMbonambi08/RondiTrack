// Startup work: apply migrations, then (if Seed:Enabled) create the demo data.
// Both run under one PostgreSQL advisory lock, so with `--scale api=2` the replicas queue up instead
// of racing: the second one waits, then finds the users already there and only syncs its in-memory store.
// The lock is session-level, so it is always released explicitly before the connection goes back to the pool.
//
// Seed:LoadDemo (default false) adds ONE extra stokvel with 40 members and a year of contributions, for
// Lighthouse and the end-to-end tests. It is added to an existing database too, and only once.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Domain;
using RondiTrack.Jobs;
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Data;

public static class DatabaseInitializer
{
    // Any constant works as long as every replica uses the same one.
    private const long StartupLockKey = 724_600_001;

    public const string LoadDemoStokvelName = "Load Test Stokvel";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<RondiTrackDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RondiTrack.DatabaseInitializer");

        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0})", StartupLockKey);
            try
            {
                await db.Database.MigrateAsync();
                await SyncOrSeedAsync(sp, db, config, logger);
                await SeedLoadDemoAsync(sp, db, config, logger);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", StartupLockKey);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task SyncOrSeedAsync(
        IServiceProvider sp, RondiTrackDbContext db, IConfiguration config, ILogger logger)
    {
        var inMemoryUsers = sp.GetRequiredService<InMemoryUserRepository>();
        var seedEnabled = config.GetValue("Seed:Enabled", true);
        var usersInDb = await db.Users.AsNoTracking().ToListAsync();

        if (usersInDb.Count > 0 || !seedEnabled)
        {
            // The database is the source of truth. The in-memory seed users get new Ids on every start,
            // so swap them for the real rows.
            foreach (var inMemory in (await inMemoryUsers.GetAllAsync()).ToList())
                await inMemoryUsers.DeleteAsync(inMemory.Id);
            foreach (var existing in usersInDb)
                await inMemoryUsers.AddAsync(existing);

            if (usersInDb.Count == 0)
                logger.LogInformation("Seed:Enabled is false and the database is empty: no demo data created.");
            return;
        }

        var time = sp.GetRequiredService<TimeProvider>();
        var hasher = sp.GetRequiredService<IPasswordHasher<User>>();
        var cycleOptions = sp.GetRequiredService<IOptions<CycleCloseOptions>>().Value;

        var now = time.GetUtcNow();
        var joinedAtUtc = now.UtcDateTime;
        var today = CycleCalendar.Today(now, cycleOptions.ResolveTimeZone());

        // The three users InMemoryUserRepository seeds, plus two more so each stokvel has its own admin.
        var seeded = (await inMemoryUsers.GetAllAsync()).ToList();
        var thandiwe = seeded.Single(u => u.Email == "thandiwe@example.com");
        var sipho = seeded.Single(u => u.Email == "sipho@example.com");
        var lindiwe = seeded.Single(u => u.Email == "lindiwe@example.com");   // inactive: cannot log in
        var nomsa = new User("Nomsa Khumalo", "nomsa@example.com");
        var admin = new User("Demo Admin", "admin@example.com") { IsPlatformAdmin = true };
        await inMemoryUsers.AddAsync(nomsa);
        await inMemoryUsers.AddAsync(admin);

        var allUsers = new[] { thandiwe, sipho, lindiwe, nomsa, admin };

        // The password comes from configuration (SEED_DEMO_PASSWORD in .env), never from the repository.
        var demoPassword = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
            logger.LogWarning("Seed:DemoPassword is not set: demo accounts are created without a password, so nobody can log in as them.");
        else
            foreach (var user in allUsers)
                user.SetPasswordHash(hasher.HashPassword(user, demoPassword));

        // Sipho belongs to BOTH stokvels (Member in one, Treasurer in the other).
        var ubuntu = new Stokvel("Ubuntu Savings Circle", 500m);
        ubuntu.AddMember(thandiwe);
        ubuntu.AddMember(sipho);

        var umoja = new Stokvel("Umoja Family Fund", 300m);
        umoja.AddMember(nomsa);
        umoja.AddMember(sipho);

        db.Users.AddRange(allUsers);
        db.Stokvels.AddRange(ubuntu, umoja);
        db.StokvelMembers.AddRange(
            new StokvelMember(ubuntu.Id, thandiwe.Id, StokvelMemberRole.Admin, joinedAtUtc),
            new StokvelMember(ubuntu.Id, sipho.Id, StokvelMemberRole.Member, joinedAtUtc),
            new StokvelMember(umoja.Id, nomsa.Id, StokvelMemberRole.Admin, joinedAtUtc),
            new StokvelMember(umoja.Id, sipho.Id, StokvelMemberRole.Treasurer, joinedAtUtc));

        // One open cycle each, covering today, so the cycle-close job has something to do when it ends.
        var (start, end) = CycleCalendar.PeriodContaining(today, cycleOptions.CycleEndDay);
        var ubuntuCycle = new ContributionCycle(
            ubuntu.Id, CycleCalendar.Label(end), ubuntu.ContributionAmount, start, end);
        var umojaCycle = new ContributionCycle(
            umoja.Id, CycleCalendar.Label(end), umoja.ContributionAmount, start, end);
        db.ContributionCycles.AddRange(ubuntuCycle, umojaCycle);

        // Umoja history, one cycle of each kind: August is PaidOut (the pot went to the recipient),
        // September is Closed (closed by the cycle-close job, Sipho short), October is the Open one above.
        // Both older cycles are WITHOUT dates (the cycle-close job ignores those).
        var umojaAugust = new ContributionCycle(umoja.Id, "2026-08", umoja.ContributionAmount);
        var umojaSeptember = new ContributionCycle(umoja.Id, "2026-09", umoja.ContributionAmount);
        var septemberClosedAtUtc = new DateTime(2026, 10, 1, 0, 5, 0, DateTimeKind.Utc);
        umojaAugust.MarkPayoutProcessed();
        umojaSeptember.MarkClosedForSeed(septemberClosedAtUtc);
        db.ContributionCycles.AddRange(umojaAugust, umojaSeptember);

        // Sipho's contributions in Umoja: all three statuses, a 10-cent one, and the midnight one:
        // paid at 00:30 on 1 November in Johannesburg, which is 2026-10-31T22:30:00Z.
        // Ubuntu has none, so Sipho still owes there; in Umoja he is paid up for the current cycle.
        db.Contributions.AddRange(
            new Contribution(umoja.Id, sipho.Id, umojaAugust.Id, 300m,
                new DateTime(2026, 8, 28, 9, 15, 0, DateTimeKind.Utc), ContributionStatus.Confirmed),
            new Contribution(umoja.Id, sipho.Id, umojaSeptember.Id, 0.10m,
                new DateTime(2026, 9, 29, 14, 5, 0, DateTimeKind.Utc), ContributionStatus.Pending),
            new Contribution(umoja.Id, sipho.Id, umojaCycle.Id, 300m,
                new DateTime(2026, 10, 31, 22, 30, 0, DateTimeKind.Utc), ContributionStatus.Paid));

        // Sipho is in arrears in Umoja: September needed R 300,00 and he paid R 0,10 = 29 990 cents short.
        // Same shape the cycle-close job records.
        db.Arrears.Add(new Arrear(umojaSeptember.Id, umoja.Id, sipho.Id, 29990, septemberClosedAtUtc));

        // One SaveChanges: the whole seed is all-or-nothing.
        await db.SaveChangesAsync();
    }

    // 40 members (39 new people plus Sipho as Treasurer), 12 cycles, a contribution per member per cycle.
    // Only runs when Seed:LoadDemo is true, and only if the stokvel is not there yet.
    private static async Task SeedLoadDemoAsync(
        IServiceProvider sp, RondiTrackDbContext db, IConfiguration config, ILogger logger)
    {
        if (!config.GetValue("Seed:LoadDemo", false)) return;
        if (await db.Stokvels.AsNoTracking().AnyAsync(s => s.Name == LoadDemoStokvelName)) return;

        var sipho = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == "sipho@example.com");
        if (sipho is null)
        {
            logger.LogWarning("Seed:LoadDemo needs the demo user sipho@example.com, which does not exist. Skipped.");
            return;
        }

        var time = sp.GetRequiredService<TimeProvider>();
        var cycleOptions = sp.GetRequiredService<IOptions<CycleCloseOptions>>().Value;
        var inMemoryUsers = sp.GetRequiredService<InMemoryUserRepository>();
        var now = time.GetUtcNow();
        var today = CycleCalendar.Today(now, cycleOptions.ResolveTimeZone());
        var joinedAtUtc = now.UtcDateTime.AddDays(-365);

        var stokvel = new Stokvel(LoadDemoStokvelName, 500m);
        var people = Enumerable.Range(1, 39)
            .Select(i => new User($"Load Member {i:00}", $"load{i:00}@example.com"))
            .ToList();

        db.Users.AddRange(people);
        db.Stokvels.Add(stokvel);
        db.StokvelMembers.Add(new StokvelMember(stokvel.Id, sipho.Id, StokvelMemberRole.Treasurer, joinedAtUtc));
        foreach (var p in people)
            db.StokvelMembers.Add(new StokvelMember(stokvel.Id, p.Id, StokvelMemberRole.Member, joinedAtUtc));

        var everyone = people.Select(p => p.Id).Append(sipho.Id).ToList();
        var (_, currentEnd) = CycleCalendar.PeriodContaining(today, cycleOptions.CycleEndDay);

        for (var k = 11; k >= 0; k--)
        {
            var end = currentEnd.AddMonths(-k);
            var start = currentEnd.AddMonths(-k - 1).AddDays(1);
            var cycle = new ContributionCycle(stokvel.Id, CycleCalendar.Label(end), 500m, start, end);
            var isCurrent = k == 0;
            if (!isCurrent)
                cycle.MarkClosedForSeed(end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            db.ContributionCycles.Add(cycle);

            // Older cycles: everybody paid. The current one: two thirds have paid so far.
            var payers = isCurrent ? everyone.Take(everyone.Count * 2 / 3).ToList() : everyone;
            var n = 0;
            foreach (var memberId in payers)
            {
                var paidAt = start.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc).AddDays(n++ % 10);
                db.Contributions.Add(new Contribution(
                    stokvel.Id, memberId, cycle.Id, 500m, paidAt,
                    isCurrent ? ContributionStatus.Paid : ContributionStatus.Confirmed));
            }
        }

        await db.SaveChangesAsync();
        foreach (var p in people) await inMemoryUsers.AddAsync(p);
        logger.LogInformation("Seed:LoadDemo created '{Name}' with 40 members and 12 cycles.", LoadDemoStokvelName);
    }
}