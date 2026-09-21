//The actual in-memory storage for stokvels.
using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class InMemoryStokvelRepository : IStokvelRepository
{
    private readonly ConcurrentDictionary<Guid, Stokvel> _stokvels = new();

    public Task<IReadOnlyList<Stokvel>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<Stokvel>)_stokvels.Values.ToList());

    public Task<Stokvel?> GetByIdAsync(Guid id)
        => Task.FromResult(_stokvels.GetValueOrDefault(id));

    public Task AddAsync(Stokvel stokvel)
    {
        _stokvels[stokvel.Id] = stokvel;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _stokvels.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}