// Just stages the add here — the actual SaveChangesAsync for a payout happens
// inside PayoutService's explicit transaction, alongside the cycle update.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class EfPayoutRepository : IPayoutRepository
{
    private readonly RondiTrackDbContext _db;

    public EfPayoutRepository(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Payout>> GetByStokvelIdAsync(Guid stokvelId)
        => await _db.Payouts.Where(p => p.StokvelId == stokvelId).ToListAsync();

    public Task AddAsync(Payout payout)
    {
        _db.Payouts.Add(payout);
        return Task.CompletedTask;
    }
}
