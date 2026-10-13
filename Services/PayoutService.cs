// Processes one payout for one cycle, inside an explicit transaction. Two
// writes have to succeed together, or neither should exist: the new Payout
// row, and marking the cycle as processed. If anything fails in between,
// the whole thing rolls back and the database looks exactly like it did
// before this call started.
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Telemetry;

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
//ProcessPayoutAsync owns telemetry, the span, tags and metric
    public async Task<PayoutResponse> ProcessPayoutAsync(Guid stokvelId, Guid contributionCycleId) //split ProcessPayoutAsync and ProcessCoreAsync
    {
        // Started before the first query, so every SQL span below nests under this one.
        using var activity = RondiTrackTelemetry.Source.StartActivity("payout.process");
        activity?.SetTag("payout.cycle_id", contributionCycleId.ToString()); //payout.cycle_id can be looked up in the database

        try
        {
            var response = await ProcessCoreAsync(stokvelId, contributionCycleId); 
            activity?.SetTag("payout.outcome", "success");
            RondiTrackTelemetry.PayoutProcessed(PayoutOutcome.Success);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetTag("payout.outcome", "failed");
            activity?.SetTag("error.type", ex.GetType().Name);
            // No description and no RecordException: exception messages can contain names.
            activity?.SetStatus(ActivityStatusCode.Error);
            RondiTrackTelemetry.PayoutProcessed(PayoutOutcome.Failed);
            throw;
        }
    }
//ProcessCoreAsync holds keeps the logic readable and means there’s one place where success or failure is recorded
    private async Task<PayoutResponse> ProcessCoreAsync(Guid stokvelId, Guid contributionCycleId)
    {
        var stokvel = await _stokvelRepo.GetByIdAsync(stokvelId)
            ?? throw new NotFoundException("Stokvel not found."); //used throw, not throw ex because rethrows the same exception and keeps the stack trace

        var cycle = await _cycleRepo.GetByIdAsync(contributionCycleId)
            ?? throw new NotFoundException("Contribution cycle not found.");

        if (cycle.StokvelId != stokvelId)
            throw new NotFoundException("That contribution cycle does not belong to this stokvel.");

        if (cycle.Status == Domain.CycleStatus.PayoutProcessed)
            throw new ConflictException("This cycle's payout has already been processed.");

        var pastRecipients = (await _payoutRepo.GetByStokvelIdAsync(stokvelId))
            .Select(p => p.RecipientUserId)
            .ToHashSet();

        var recipientId = stokvel.MemberIds.FirstOrDefault(id => !pastRecipients.Contains(id));
        if (recipientId == Guid.Empty)
            throw new ConflictException("No eligible recipient remains for this stokvel.");

        // EnableRetryOnFailure means a manually opened transaction can't just be
        // awaited directly, it has to run inside the execution strategy's own
        // retry-aware wrapper, or EF throws InvalidOperationException at runtime.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var payout = new Domain.Payout(stokvelId, contributionCycleId, recipientId, cycle.TargetAmount);
                await _payoutRepo.AddAsync(payout);
                await _db.SaveChangesAsync();

                TestHookBeforeCycleUpdate?.Invoke(); //TestHookBeforeCycleUpdate , a test to force a failure between the two writes and prove the rollback works

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
        });
    }
}