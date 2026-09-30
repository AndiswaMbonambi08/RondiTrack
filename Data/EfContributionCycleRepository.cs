// The real, database-backed version of IContributionCycleRepository. Swapped
// alongside Stokvel because payout processing needs to update a cycle and
// create a Payout inside the same transaction — only possible if they share
// the same DbContext.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class EfContributionCycleRepository : IContributionCycleRepository
{
    private readonly RondiTrackDbContext _db;

    public EfContributionCycleRepository(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ContributionCycle>> GetAllAsync()
        => await _db.ContributionCycles.ToListAsync();

    public async Task<ContributionCycle?> GetByIdAsync(Guid id)
        => await _db.ContributionCycles.FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(ContributionCycle cycle)
    {
        _db.ContributionCycles.Add(cycle);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var cycle = await _db.ContributionCycles.FindAsync(id);
        if (cycle is not null)
        {
            _db.ContributionCycles.Remove(cycle);
            await _db.SaveChangesAsync();
        }
    }
}