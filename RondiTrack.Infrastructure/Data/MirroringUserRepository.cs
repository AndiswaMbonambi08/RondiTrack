using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

// Users still live in InMemoryUserRepository (a stated decision). The StokvelMembers table has a
// foreign key to the Users table, so each user is also inserted into that table (insert-if-missing).
// Updates and deletes are not mirrored; the table row only exists to satisfy the foreign key.
public class MirroringUserRepository(InMemoryUserRepository inner, RondiTrackDbContext db) : IUserRepository
{
    public Task<IReadOnlyList<User>> GetAllAsync() => inner.GetAllAsync();

    public Task<User?> GetByIdAsync(Guid id) => inner.GetByIdAsync(id);

public async Task AddAsync(User user)
{
    await inner.AddAsync(user);
    if (!await db.Users.AnyAsync(u => u.Id == user.Id))
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();   // commit now, so the row exists before the response is returned
    }
}
    public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
}