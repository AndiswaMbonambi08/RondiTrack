// Startup work: apply migrations, then (if Seed:Enabled) create the demo data.
// Both run under one PostgreSQL advisory lock, so with `--scale api=2` the replicas queue up instead
// of racing: the second one waits, then finds the users already there and only syncs its in-memory store.
// The lock is session-level, so it is always released explicitly before the connection goes back to the pool.
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
        foreach (var stokvel in new[] { ubuntu, umoja })
            db.ContributionCycles.Add(new ContributionCycle(
                stokvel.Id, CycleCalendar.Label(end), stokvel.ContributionAmount, start, end));

        // One SaveChanges: the whole seed is all-or-nothing.
        await db.SaveChangesAsync();
    }
}