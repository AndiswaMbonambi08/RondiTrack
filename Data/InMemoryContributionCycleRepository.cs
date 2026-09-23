// In-memory storage for ContributionCycles, same pattern as the other repositories.
using System.Collections.Concurrent;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public class InMemoryContributionCycleRepository : IContributionCycleRepository
{
    private readonly ConcurrentDictionary<Guid, ContributionCycle> _cycles = new();

    public Task<IReadOnlyList<ContributionCycle>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<ContributionCycle>)_cycles.Values.ToList());

    public Task<ContributionCycle?> GetByIdAsync(Guid id)
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