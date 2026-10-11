using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class EfUserCredentialStore(RondiTrackDbContext db) : IUserCredentialStore
{
    public Task<bool> EmailExistsAsync(string normalizedEmail) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail);

    public Task<User?> FindByEmailAsync(string normalizedEmail) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail);

    public Task<User?> FindByIdAsync(Guid id) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
}