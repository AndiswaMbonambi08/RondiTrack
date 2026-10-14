// Deletes data that has outlived the feature it protected. Safe to run twice or on two replicas:
// deleting rows that are already gone does nothing. Set-based (ExecuteDeleteAsync), nothing is loaded.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Data;
using RondiTrack.Telemetry;

namespace RondiTrack.Jobs;

public sealed record HousekeepingRunResult(int RefreshTokensDeleted, int IdempotencyEntriesDeleted);

public class HousekeepingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly HousekeepingOptions _options;
    private readonly ILogger<HousekeepingJob> _logger;

    public HousekeepingJob(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<HousekeepingOptions> options,
        ILogger<HousekeepingJob> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Housekeeping job is disabled (Housekeeping:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes), _time);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Housekeeping run failed; it will be retried on the next tick.");
            }
        }
        while (await NextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> NextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    public async Task<HousekeepingRunResult> RunOnceAsync(CancellationToken ct = default)
    {
        using var activity = RondiTrackTelemetry.Source.StartActivity("housekeeping.run");

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var tokenCutoff = nowUtc.AddDays(-_options.RefreshTokenRetentionDays);
        var idempotencyCutoff = nowUtc.AddHours(-_options.IdempotencyRetentionHours);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        // Keyed on EXPIRY, not creation or use: a spent token must stay around for N days after it
        // expires, because that row is what lets reuse detection recognise a replay.
        var tokens = await db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < tokenCutoff)
            .ExecuteDeleteAsync(ct);

        var entries = await db.IdempotencyEntries
            .Where(e => e.CreatedAtUtc < idempotencyCutoff)
            .ExecuteDeleteAsync(ct);

        RondiTrackTelemetry.HousekeepingRowsDeleted(HousekeepingKind.RefreshTokens, tokens);
        RondiTrackTelemetry.HousekeepingRowsDeleted(HousekeepingKind.IdempotencyEntries, entries);

        _logger.LogInformation(
            "Housekeeping finished. RefreshTokensDeleted: {RefreshTokensDeleted}, IdempotencyEntriesDeleted: {IdempotencyEntriesDeleted}",
            tokens, entries);

        activity?.SetTag("refresh_tokens.deleted", tokens);
        activity?.SetTag("idempotency_entries.deleted", entries);

        return new HousekeepingRunResult(tokens, entries);
    }
}