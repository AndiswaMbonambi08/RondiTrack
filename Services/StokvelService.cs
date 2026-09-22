// Owns RondiTrack's cross-entity decisions. Fetches from repositories, calls the domain
// entity to enforce its own rules, and owns the idempotency check for contributions.
// Endpoints call this instead of talking to repositories and entities directly.
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;

namespace RondiTrack.Services;

public class StokvelService : IStokvelService
{
    private readonly IStokvelRepository _stokvelRepo;
    private readonly IUserRepository _userRepo;
    private readonly IIdempotencyStore _idempotencyStore;

    public StokvelService(IStokvelRepository stokvelRepo, IUserRepository userRepo, IIdempotencyStore idempotencyStore)
    {
        _stokvelRepo = stokvelRepo;
        _userRepo = userRepo;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<StokvelResponse> AddMemberAsync(Guid stokvelId, Guid userId)
    {
        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new ResourceNotFoundException("Stokvel not found.");

        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new ResourceNotFoundException("User not found.");

        stokvel.AddMember(user);

        return stokvel.ToResponse();
    }

    public async Task<ContributionResponse> RecordContributionAsync(Guid stokvelId, RecordContributionRequest request, string idempotencyKey)
    {
        var existing = await _idempotencyStore.GetAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.Request != request)
                throw new IdempotencyMismatchException("This idempotency key was already used with a different request.");

            return existing.Response;
        }

        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new ResourceNotFoundException("Stokvel not found.");

        var user = await _userRepo.GetByIdAsync(request.UserId)
            ?? throw new ResourceNotFoundException("User not found.");

        var contribution = stokvel.RecordContribution(user, request.Cycle, request.Amount);
        var response = contribution.ToResponse(stokvelId);

        await _idempotencyStore.SaveAsync(idempotencyKey, new IdempotencyRecord(request, response));

        return response;
    }
}