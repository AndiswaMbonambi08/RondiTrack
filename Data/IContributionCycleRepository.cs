// Contract for storing ContributionCycles.
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IContributionCycleRepository
{
    Task<IReadOnlyList<ContributionCycle>> GetAllAsync(bool asNoTracking = false);
    Task<ContributionCycle?> GetByIdAsync(Guid id, bool asNoTracking = false);
    Task AddAsync(ContributionCycle cycle);
    Task DeleteAsync(Guid id);
}