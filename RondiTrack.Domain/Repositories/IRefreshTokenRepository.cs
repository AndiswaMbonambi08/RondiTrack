using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<RefreshToken?> FindByHashAsync(string tokenHash);

    // Claims a token for rotation. Returns true for exactly one caller, even if two
    // requests present the same token at the same instant.
    Task<bool> TryMarkUsedAsync(Guid id, DateTime nowUtc);
    // Revokes every token that descends from one login.
    Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc);
}