//Contract for storing/retrieving stokvels.
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IStokvelRepository
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync();
    Task<Stokvel?> GetByIdAsync(Guid id);
    Task AddAsync(Stokvel stokvel);
    Task DeleteAsync(Guid id);
}