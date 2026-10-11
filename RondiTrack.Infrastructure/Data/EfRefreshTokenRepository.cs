using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class EfRefreshTokenRepository(RondiTrackDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash) =>
        db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    // One UPDATE ... WHERE UsedAtUtc IS NULL. The database decides who wins a race, not our code.
    public async Task<bool> TryMarkUsedAsync(Guid id, DateTime nowUtc)
    {
        var rows = await db.RefreshTokens
            .Where(t => t.Id == id && t.UsedAtUtc == null && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, nowUtc));
        return rows == 1;
    }

    // Runs immediately as its own statement, so it sticks even though the request that
    // triggered it goes on to fail with a 401.
    public Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, nowUtc));
}
