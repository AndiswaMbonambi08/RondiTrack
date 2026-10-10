//Contract for storing/retrieving stokvels.
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IStokvelRepository
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync(bool asNoTracking = false);
    Task<Stokvel?> GetByIdAsync(Guid id, bool asNoTracking = false);
    Task AddAsync(Stokvel stokvel);
    Task DeleteAsync(Guid id);
    Task LoadContributionsAsync(Stokvel stokvel, Guid contributionCycleId);
    void AddContribution(Contribution contribution);
}