using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IUserCredentialStore
{
    Task<bool> EmailExistsAsync(string normalizedEmail);
    Task<User?> FindByEmailAsync(string normalizedEmail);
    Task<User?> FindByIdAsync(Guid id);
}