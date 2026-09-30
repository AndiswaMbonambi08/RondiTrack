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
            // This is the property that wouldn't map cleanly. It's a computed,
            // read-only wrapper over a private field with no public setter, so
            // EF has no way to write to it. We tell EF to skip it entirely and
            // use the StokvelMember table as the real source of truth instead.
            b.Ignore(s => s.MemberIds);
        });

        modelBuilder.Entity<StokvelMember>(b =>
        {
            b.HasKey(sm => new { sm.StokvelId, sm.UserId });
        });

        modelBuilder.Entity<ContributionCycle>(b => b.HasKey(c => c.Id));
        modelBuilder.Entity<Contribution>(b => b.HasKey(c => c.Id));
        modelBuilder.Entity<Payout>(b => b.HasKey(p => p.Id));
    }
}