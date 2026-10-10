// In-memory storage for ContributionCycles, same pattern as the other repositories.
// No longer registered anywhere (replaced by EfContributionCycleRepository as of
// Assignment 5.1), kept in the codebase as a stated decision rather than deleted,
// but still needs to satisfy the interface's current shape to compile.
using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class InMemoryContributionCycleRepository : IContributionCycleRepository
{
    private readonly ConcurrentDictionary<Guid, ContributionCycle> _cycles = new();

    public Task<IReadOnlyList<ContributionCycle>> GetAllAsync(bool asNoTracking = false)
        => Task.FromResult((IReadOnlyList<ContributionCycle>)_cycles.Values.ToList());

    public Task<ContributionCycle?> GetByIdAsync(Guid id, bool asNoTracking = false)
        => Task.FromResult(_cycles.GetValueOrDefault(id));

    public Task AddAsync(ContributionCycle cycle)
    {
        _cycles[cycle.Id] = cycle;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _cycles.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}