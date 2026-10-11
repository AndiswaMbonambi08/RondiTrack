using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Authorization;

public interface IStokvelMembershipLookup
{
    Task<StokvelMemberRole?> GetRoleAsync(Guid stokvelId, Guid userId, CancellationToken ct);
}

public sealed class StokvelMembershipLookup(RondiTrackDbContext db) : IStokvelMembershipLookup
{
    public async Task<StokvelMemberRole?> GetRoleAsync(Guid stokvelId, Guid userId, CancellationToken ct) =>
        await db.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId && m.UserId == userId)
            .Select(m => (StokvelMemberRole?)m.Role)
            .FirstOrDefaultAsync(ct);
}