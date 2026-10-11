using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IUserCredentialStore
{
    Task<bool> EmailExistsAsync(string normalizedEmail);
    Task<User?> FindByEmailAsync(string normalizedEmail);
    public Task<User?> FindByIdAsync(Guid id) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
}