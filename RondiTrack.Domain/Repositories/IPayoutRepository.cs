using RondiTrack.Domain;

namespace RondiTrack.Data;

public interface IPayoutRepository
{
    Task<IReadOnlyList<Payout>> GetByStokvelIdAsync(Guid stokvelId);
    Task AddAsync(Payout payout);
}