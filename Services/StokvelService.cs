// Owns RondiTrack's cross-entity decisions. Fetches from repositories, calls the domain
// entity to enforce its own rules, and owns the idempotency check for contributions.
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Telemetry;

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
        var (response, _) = await RecordContributionWithReplayAsync(request.UserId, stokvelId, request, idempotencyKey);
        return response;
    }

    public async Task<(ContributionResponse Response, bool Replayed)> RecordContributionWithReplayAsync(
        Guid callerId, Guid stokvelId, RecordContributionRequest request, string idempotencyKey)
    {
        // Scoped so two users (or two stokvels) that pick the same key never collide.
        var storeKey = $"{callerId}:{stokvelId}:{idempotencyKey}";

        var existing = await _idempotencyStore.GetAsync(storeKey);
        if (existing is not null)
        {
            if (existing.Request != request)
                throw new IdempotencyConflictException("This idempotency key was already used with a different request.");

            // A replay is not a new contribution, so it is not counted.
            return (existing.Response, true);
        }

        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new NotFoundException("Stokvel not found.");

        var user = await _userRepo.GetByIdAsync(request.UserId)
            ?? throw new NotFoundException("User not found.");

        var cycle = await _cycleRepo.GetByIdAsync(request.ContributionCycleId)
            ?? throw new NotFoundException("Contribution cycle not found.");

        if (cycle.StokvelId != stokvelId)
            throw new NotFoundException("That contribution cycle does not belong to this stokvel.");

        await _stokvelRepo.LoadContributionsAsync(stokvel, cycle.Id);
        var contribution = stokvel.RecordContribution(user, cycle.Id, request.Amount);
        _stokvelRepo.AddContribution(contribution);
        var response = contribution.ToResponse();

        // Only reached once the domain accepted the contribution. A request the rate limiter
        // rejected never gets here, so a 429 can never consume a key.
        await _idempotencyStore.SaveAsync(storeKey, new IdempotencyRecord(request, response));

        // Counted once the domain accepted it. The row itself is saved by SaveChangesMiddleware afterwards.
        RondiTrackTelemetry.ContributionRecorded();

        return (response, false);
    }
}