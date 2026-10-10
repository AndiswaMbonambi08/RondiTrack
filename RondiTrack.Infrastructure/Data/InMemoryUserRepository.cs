//The actual in-memory storage for users (a ConcurrentDictionary), plus seed data.
using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public InMemoryUserRepository() => Seed();

    public Task<IReadOnlyList<User>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<User>)_users.Values.ToList());

    public Task<User?> GetByIdAsync(Guid id)
        => Task.FromResult(_users.GetValueOrDefault(id));

    public Task AddAsync(User user)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _users.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private void Seed()
    {
        var thandiwe = new User("Thandiwe Nkosi", "thandiwe@example.com");
        var sipho = new User("Sipho Dlamini", "sipho@example.com");
        var lindiwe = new User("Lindiwe Zulu", "lindiwe@example.com");
        lindiwe.Deactivate();

        foreach (var user in new[] { thandiwe, sipho, lindiwe })
            _users[user.Id] = user;
    }
}