using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Jobs;
using RondiTrack.Services;

namespace RondiTrack.Endpoints;

public record CloseCycleBody(string? Version);
public record ArrearsItem(Guid MemberId, string MemberName, long ShortfallCents);
public record ClosePreviewResponse(
    Guid CycleId, string CycleLabel, DateOnly AsOf, long TotalArrearsCents,
    IReadOnlyList<ArrearsItem> Items, string Version);
public record CloseCycleResponse(
    Guid CycleId, string CycleLabel, DateTime ClosedAtUtc, DateOnly AsOf, int ArrearsCount, long TotalArrearsCents);

public static class CloseEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapCloseEndpoints(this WebApplication app)
    {
        var route = ApiRoutes.Stokvels + "/{stokvelId:guid}/cycles/{cycleId:guid}";

        app.MapGet(route + "/close-preview", async (
            Guid stokvelId, Guid cycleId, HttpContext http, RondiTrackDbContext db,
            IOptions<CycleCloseOptions> options, TimeProvider time, CancellationToken ct) =>
        {
            var cycle = await FindCycleAsync(db, stokvelId, cycleId, ct);
            if (cycle.ClosedAtUtc is not null) return AlreadyClosed(http);
            return Results.Ok(await BuildPreviewAsync(db, options.Value, time, stokvelId, cycleId, cycle.Label, ct));
        })
        .RequireAuthorization(Policies.StokvelTreasurer)
        .WithTags("ContributionCycles")
        .WithSummary("Preview what closing a cycle will do")
        .WithDescription("Treasurer or Admin only. Who goes into arrears and by how much, as of which South African date, plus a version to send back when closing. Uses the same code as the real close.")
        .Produces<ClosePreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost(route + "/close", async (
            Guid stokvelId, Guid cycleId, CloseCycleBody body,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            HttpContext http, RondiTrackDbContext db,
            IOptions<CycleCloseOptions> options, TimeProvider time, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new RequestValidationException("An Idempotency-Key header is required to close a cycle.");
            if (string.IsNullOrWhiteSpace(body.Version))
                throw new RequestValidationException("version is required: close a cycle from a preview.");

            var storeKey = $"{callerId}|{cycleId}|{idempotencyKey.Trim()}";
            if (storeKey.Length > 300) throw new RequestValidationException("The Idempotency-Key is too long.");

            var done = await db.CloseRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Key == storeKey, ct);
            if (done is not null) return Replay(http, done);

            var cycle = await FindCycleAsync(db, stokvelId, cycleId, ct);
            if (cycle.ClosedAtUtc is not null) return AlreadyClosed(http);

            var preview = await BuildPreviewAsync(db, options.Value, time, stokvelId, cycleId, cycle.Label, ct);
            if (preview.Version != body.Version) return Stale(http);

            var now = time.GetUtcNow().UtcDateTime;
            var strategy = db.Database.CreateExecutionStrategy();
            var (result, stale) = await strategy.ExecuteAsync<(CloseCycleResponse? Result, bool Stale)>(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                // The database picks the winner, exactly as the cycle-close job does.
                var claimed = await db.ContributionCycles
                    .Where(c => c.Id == cycleId && c.StokvelId == stokvelId && c.ClosedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, now), ct);
                if (claimed == 0)
                {
                    await tx.RollbackAsync(ct);
                    return (null, false);
                }

                // Counted again inside the transaction: what is recorded is what the version described.
                var again = await BuildPreviewAsync(db, options.Value, time, stokvelId, cycleId, cycle.Label, ct);
                if (again.Version != body.Version)
                {
                    await tx.RollbackAsync(ct);
                    return (null, true);
                }

                foreach (var item in again.Items)
                    db.Arrears.Add(new Arrear(cycleId, stokvelId, item.MemberId, item.ShortfallCents, now));

                var response = new CloseCycleResponse(
                    cycleId, cycle.Label, now, again.AsOf, again.Items.Count, again.TotalArrearsCents);
                db.CloseRequests.Add(new CloseRequest(storeKey, cycleId, JsonSerializer.Serialize(response, Json), now));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return (response, false);
            });

            if (stale) return Stale(http);
            if (result is null)
            {
                // Lost the race. If it was the same request, answer with the first result.
                var first = await db.CloseRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Key == storeKey, ct);
                return first is not null ? Replay(http, first) : AlreadyClosed(http);
            }
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.StokvelTreasurer)
        .WithTags("ContributionCycles")
        .WithSummary("Close a cycle exactly as previewed")
        .WithDescription("Treasurer or Admin only. Needs an Idempotency-Key and the version from the preview. 409 (code stale-preview) if anything changed since; 409 (code already-closed) if closed. The same key answers with the first result and Idempotency-Replayed: true.")
        .Produces<CloseCycleResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static IResult Stale(HttpContext http) =>
        CodedProblems.Make(http, 409, "The preview is out of date",
            "Something changed since you opened the preview. Check the new numbers and confirm again.", "stale-preview");

    private static IResult AlreadyClosed(HttpContext http) =>
        CodedProblems.Make(http, 409, "Cycle already closed", "This cycle has already been closed.", "already-closed");

    private static IResult Replay(HttpContext http, CloseRequest first)
    {
        http.Response.Headers["Idempotency-Replayed"] = "true";
        return Results.Content(first.ResponseJson, "application/json");
    }

    private static async Task<(string Label, DateTime? ClosedAtUtc)> FindCycleAsync(
        RondiTrackDbContext db, Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var cycle = await db.ContributionCycles.AsNoTracking()
            .Where(c => c.Id == cycleId && c.StokvelId == stokvelId)
            .Select(c => new { c.Label, c.ClosedAtUtc })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Contribution cycle not found.");
        return (cycle.Label, cycle.ClosedAtUtc);
    }

    private static async Task<ClosePreviewResponse> BuildPreviewAsync(
        RondiTrackDbContext db, CycleCloseOptions options, TimeProvider time,
        Guid stokvelId, Guid cycleId, string label, CancellationToken ct)
    {
        var zone = options.ResolveTimeZone();
        var asOf = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, zone));

        var shortfalls = await ArrearsCalculator.ComputeAsync(db, cycleId, stokvelId, ct);
        var ids = shortfalls.Select(s => s.UserId).ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var items = shortfalls
            .Select(s => new ArrearsItem(s.UserId, names.GetValueOrDefault(s.UserId) ?? "A member", s.Cents))
            .OrderBy(i => i.MemberName, StringComparer.Ordinal).ThenBy(i => i.MemberId)
            .ToList();
        var total = items.Sum(i => i.ShortfallCents);

        // The version describes WHAT will be recorded (who and how much), so any change to it makes the preview stale.
        var raw = $"{cycleId}|" + string.Join(';', items.OrderBy(i => i.MemberId).Select(i => $"{i.MemberId}:{i.ShortfallCents}"));
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];

        return new ClosePreviewResponse(cycleId, label, asOf, total, items, version);
    }
}