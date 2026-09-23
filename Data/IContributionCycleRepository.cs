// Contract for storing ContributionCycles.
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IContributionCycleRepository
{
    Task<IReadOnlyList<ContributionCycle>> GetAllAsync();
    Task<ContributionCycle?> GetByIdAsync(Guid id);
    Task AddAsync(ContributionCycle cycle);
    Task DeleteAsync(Guid id);
}