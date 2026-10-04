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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b => b.HasKey(u => u.Id));

        modelBuilder.Entity<Stokvel>(b =>
        {
            b.HasKey(s => s.Id);
            b.Ignore(s => s.MemberIds);
        });

        modelBuilder.Entity<StokvelMember>(b =>
        {
            // The composite primary key itself — the direct answer to
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
        });

        modelBuilder.Entity<Contribution>(b =>
        {
            b.HasKey(c => c.Id);

            // The composite FK answering the "how does Contribution reference
            // a specific membership" question from Phase 3 — points at
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
        });

        modelBuilder.Entity<Payout>(b => b.HasKey(p => p.Id));
    }
}