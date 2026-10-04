// The real, database-backed version of IStokvelRepository. Same interface as
// the in-memory one that came before it — nothing above this had to change.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class EfStokvelRepository : IStokvelRepository
{
    private readonly RondiTrackDbContext _db;

    public EfStokvelRepository(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Stokvel>> GetAllAsync(bool asNoTracking = false)
    {
        var query = _db.Stokvels.AsQueryable();
        if (asNoTracking) query = query.AsNoTracking();
        var stokvels = await query.ToListAsync();
        foreach (var stokvel in stokvels)
            await LoadMembersAsync(stokvel);
        return stokvels;
    }

    public async Task<Stokvel?> GetByIdAsync(Guid id, bool asNoTracking = false)
    {
        var query = _db.Stokvels.AsQueryable();
        if (asNoTracking) query = query.AsNoTracking();
        var stokvel = await query.FirstOrDefaultAsync(s => s.Id == id);
        if (stokvel is not null)
            await LoadMembersAsync(stokvel);
        return stokvel;
    }

    public async Task AddAsync(Stokvel stokvel)
    {
        _db.Stokvels.Add(stokvel);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var stokvel = await _db.Stokvels.FindAsync(id);
        if (stokvel is not null)
        {
            _db.Stokvels.Remove(stokvel);
            await _db.SaveChangesAsync();
        }
    }

    // Loads this stokvel's rows from the join table and puts them back into
    // the domain object's in-memory list using the internal loader method.
    private async Task LoadMembersAsync(Stokvel stokvel)
    {
        var memberIds = await _db.StokvelMembers
            .Where(sm => sm.StokvelId == stokvel.Id)
            .Select(sm => sm.UserId)
            .ToListAsync();
        stokvel.LoadMembers(memberIds);
    }
}