// Processes one payout for one cycle, inside an explicit transaction. Two
// writes have to succeed together, or neither should exist: the new Payout
// row, and marking the cycle as processed. If anything fails in between,
// the whole thing rolls back and the database looks exactly like it did
// before this call started.
using Microsoft.EntityFrameworkCore.Storage;
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;

namespace RondiTrack.Services;

public class PayoutService : IPayoutService
{
    private readonly RondiTrackDbContext _db;
    private readonly IStokvelRepository _stokvelRepo;
    private readonly IContributionCycleRepository _cycleRepo;
    private readonly IPayoutRepository _payoutRepo;

    // Test-only hook: lets a test force a failure between the two writes,
    // to prove the transaction actually rolls both back together. Never
    // set outside a test.
    internal Action? TestHookBeforeCycleUpdate { get; set; }

    public PayoutService(RondiTrackDbContext db, IStokvelRepository stokvelRepo, IContributionCycleRepository cycleRepo, IPayoutRepository payoutRepo)
    {
        _db = db;
        _stokvelRepo = stokvelRepo;
        _cycleRepo = cycleRepo;
        _payoutRepo = payoutRepo;
    }

    public async Task<PayoutResponse> ProcessPayoutAsync(Guid stokvelId, Guid contributionCycleId)
    {
        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new NotFoundException("Stokvel not found.");

        var cycle = await _cycleRepo.GetByIdAsync(contributionCycleId)
            ?? throw new NotFoundException("Contribution cycle not found.");

        if (cycle.StokvelId != stokvelId)
            throw new NotFoundException("That contribution cycle does not belong to this stokvel.");

        if (cycle.Status == Domain.CycleStatus.PayoutProcessed)
            throw new ConflictException("This cycle's payout has already been processed.");

        // Rotation rule: the earliest-joined member who has never received
        // a payout from this stokvel yet.
        var pastRecipients = (await _payoutRepo.GetByStokvelIdAsync(stokvelId))
            .Select(p => p.RecipientUserId)
            .ToHashSet();

        var recipientId = stokvel.MemberIds.FirstOrDefault(id => !pastRecipients.Contains(id));
        if (recipientId == Guid.Empty)
            throw new ConflictException("No eligible recipient remains for this stokvel.");

        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var payout = new Domain.Payout(stokvelId, contributionCycleId, recipientId, cycle.TargetAmount);
            await _payoutRepo.AddAsync(payout);
            await _db.SaveChangesAsync();

            TestHookBeforeCycleUpdate?.Invoke();

            cycle.MarkPayoutProcessed();
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return payout.ToResponse();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}