// Owns RondiTrack's cross-entity decisions. Fetches from repositories, calls the domain
// entity to enforce its own rules, and owns the idempotency check for contributions.
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;

namespace RondiTrack.Services;

public class StokvelService : IStokvelService
{
    private readonly IStokvelRepository _stokvelRepo;
    private readonly IUserRepository _userRepo;
    private readonly IContributionCycleRepository _cycleRepo;
    private readonly IIdempotencyStore _idempotencyStore;

    public StokvelService(
        IStokvelRepository stokvelRepo,
        IUserRepository userRepo,
        IContributionCycleRepository cycleRepo,
        IIdempotencyStore idempotencyStore)
    {
        _stokvelRepo = stokvelRepo;
        _userRepo = userRepo;
        _cycleRepo = cycleRepo;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<StokvelResponse> AddMemberAsync(Guid stokvelId, Guid userId)
    {
        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new NotFoundException("Stokvel not found.");

        var user = await _userRepo.GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        stokvel.AddMember(user);

        return stokvel.ToResponse();
    }

    public async Task<ContributionResponse> RecordContributionAsync(Guid stokvelId, RecordContributionRequest request, string idempotencyKey)
    {
        var existing = await _idempotencyStore.GetAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.Request != request)
                throw new IdempotencyConflictException("This idempotency key was already used with a different request.");

            return existing.Response;
        }

        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new NotFoundException("Stokvel not found.");

        var user = await _userRepo.GetByIdAsync(request.UserId)
            ?? throw new NotFoundException("User not found.");

        var cycle = await _cycleRepo.GetByIdAsync(request.ContributionCycleId)
            ?? throw new NotFoundException("Contribution cycle not found.");

        if (cycle.StokvelId != stokvelId)
            throw new NotFoundException("That contribution cycle does not belong to this stokvel.");

        var contribution = stokvel.RecordContribution(user, cycle.Id, request.Amount);
        var response = contribution.ToResponse(stokvelId);

        await _idempotencyStore.SaveAsync(idempotencyKey, new IdempotencyRecord(request, response));

        return response;
    }
}