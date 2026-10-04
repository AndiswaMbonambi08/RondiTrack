using Microsoft.EntityFrameworkCore;
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Data;

public class EfStokvelMemberRepository : IStokvelMemberRepository
{
    private readonly RondiTrackDbContext _db;

    public EfStokvelMemberRepository(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<StokvelMember?> GetAsync(Guid stokvelId, Guid userId)
        => await _db.StokvelMembers.FindAsync(stokvelId, userId);

    public async Task<IReadOnlyList<StokvelMember>> GetByStokvelIdAsync(Guid stokvelId)
        => await _db.StokvelMembers.Where(sm => sm.StokvelId == stokvelId).ToListAsync();
}