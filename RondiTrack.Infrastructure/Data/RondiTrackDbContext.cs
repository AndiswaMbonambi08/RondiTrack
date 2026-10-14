// One DbContext for the whole app. Every table RondiTrack can persist gets a
// line here, even the ones we haven't switched a repository over to use yet.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Data;

public class RondiTrackDbContext : DbContext
{
    public RondiTrackDbContext(DbContextOptions<RondiTrackDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Arrear> Arrears => Set<Arrear>();
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");
        });

        modelBuilder.Entity<Stokvel>(b =>
        {
            b.HasKey(s => s.Id);
            b.Ignore(s => s.MemberIds);
        });

        modelBuilder.Entity<StokvelMember>(b =>
        {
            // The composite primary key itself, the direct answer to
            // "configure a composite primary key via the Fluent API."
            b.HasKey(sm => new { sm.StokvelId, sm.UserId });

            b.HasOne(sm => sm.User)
                .WithMany()
                .HasForeignKey(sm => sm.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(sm => sm.Stokvel)
                .WithMany()
                .HasForeignKey(sm => sm.StokvelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContributionCycle>(b =>
        {
            b.HasKey(c => c.Id);

            // Opening the next cycle twice is impossible: one cycle per (stokvel, start date).
            // Rows without dates (legacy) have NULL start dates, which Postgres treats as distinct.
            b.HasIndex(c => new { c.StokvelId, c.StartDate })
                .IsUnique()
                .HasDatabaseName("ux_cycles_stokvel_startdate");

            // The job's "what is due?" query: open cycles ordered by end date.
            b.HasIndex(c => new { c.ClosedAtUtc, c.EndDate })
                .HasDatabaseName("ix_cycles_closed_enddate");
        });

        modelBuilder.Entity<Contribution>(b =>
        {
            b.HasKey(c => c.Id);

            // The composite FK answering the "how does Contribution reference
            // a specific membership" question from Phase 3: points at
            // StokvelMember's composite key using the same two columns.
            b.HasOne(c => c.Member)
                .WithMany(sm => sm.Contributions)
                .HasForeignKey(c => new { c.StokvelId, c.UserId })
                .OnDelete(DeleteBehavior.Restrict);

            // The one-to-many from Phase 4.
            b.HasOne(c => c.ContributionCycle)
                .WithMany(cc => cc.Contributions)
                .HasForeignKey(c => c.ContributionCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            // STEP 9 (after the "before" plan): uncomment this index.
            // WHERE StokvelId = ? AND ContributionCycleId = ? ORDER BY RecordedAt, Id
            // Equality columns first, then the ORDER BY columns in order, so rows come out already sorted.
            // b.HasIndex(c => new { c.ContributionCycleId, c.StokvelId, c.RecordedAt, c.Id })
            //     .HasDatabaseName("ix_contributions_cycle_stokvel_recordedat_id");

            b.HasIndex(c => new { c.ContributionCycleId, c.UserId })
                .IsUnique()
                .HasDatabaseName("ux_contributions_cycle_user");
        });

        modelBuilder.Entity<Payout>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Version).IsRowVersion();     // needs: public uint Version { get; private set; } on Payout
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);   // SHA-256 as hex
            b.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_hash");
            b.HasIndex(t => t.FamilyId).HasDatabaseName("ix_refresh_tokens_family");

            // Housekeeping deletes by ExpiresAtUtc.
            b.HasIndex(t => t.ExpiresAtUtc).HasDatabaseName("ix_refresh_tokens_expires");

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Arrear>(b =>
        {
            b.HasKey(a => a.Id);

            // Exactly-once for arrears: a member can have at most one arrear per cycle,
            // so even a bug or a race cannot record the same shortfall twice.
            b.HasIndex(a => new { a.ContributionCycleId, a.UserId })
                .IsUnique()
                .HasDatabaseName("ux_arrears_cycle_user");

            b.HasOne<ContributionCycle>()
                .WithMany()
                .HasForeignKey(a => a.ContributionCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IdempotencyEntry>(b =>
        {
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(200);
            b.Property(e => e.RequestJson).IsRequired();
            b.Property(e => e.ResponseJson).IsRequired();

            // Housekeeping deletes by age.
            b.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("ix_idempotency_created");
        });
    }
}