using RondiTrack.Dtos;

namespace RondiTrack.Services;

public interface IPayoutService
{
    Task<PayoutResponse> ProcessPayoutAsync(Guid stokvelId, Guid contributionCycleId);
}