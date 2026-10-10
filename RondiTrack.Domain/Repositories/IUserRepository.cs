//Contract for storing/retrieving users. Lets endpoints depend on an abstraction, not a concrete storage type.
using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task AddAsync(User user);
    Task DeleteAsync(Guid id);
}