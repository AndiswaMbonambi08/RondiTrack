// GET /api/v1/stokvels/{id}: one stokvel as seen by one of its members.
// A non-member, an unknown id and a malformed id all get the same 404, so the answer never
// confirms that a stokvel exists.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Browser;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;

namespace RondiTrack.Endpoints;

public static class StokvelDetailEndpoints
{
    public static void MapStokvelDetailEndpoints(this WebApplication app)
    {
        app.MapGet(ApiRoutes.Stokvels + "/{id}", async (
            string id,
            HttpContext http,
            IStokvelMembershipLookup membership,
            RondiTrackDbContext db,
            CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null)
                return AuthProblems.Forbidden(http);

            // Same answer for "malformed", "does not exist" and "not yours".
            if (!Guid.TryParse(id, out var stokvelId))
                throw new NotFoundException("Stokvel not found.");

            var role = await membership.GetRoleAsync(stokvelId, callerId.Value, ct);
            if (role is null)
                throw new NotFoundException("Stokvel not found.");

            var stokvel = await db.Stokvels.AsNoTracking()
                .Where(s => s.Id == stokvelId)
                .Select(s => new { s.Id, s.Name, s.ContributionAmount })
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Stokvel not found.");

            var memberCount = await db.StokvelMembers.AsNoTracking()
                .CountAsync(m => m.StokvelId == stokvelId, ct);

            // Same rule as /me/home: the newest cycle (highest label) that is not paid out.
            var cycles = await db.ContributionCycles.AsNoTracking()
                .Where(c => c.StokvelId == stokvelId && c.Status != CycleStatus.PayoutProcessed)
                .Select(c => new { c.Id, c.Label, c.EndDate, c.ClosedAtUtc })
                .ToListAsync(ct);
            var current = cycles.OrderByDescending(c => c.Label, StringComparer.Ordinal).FirstOrDefault();

            StokvelCycleSummary? summary = null;
            if (current is not null)
            {
                var paid = await db.Contributions.AsNoTracking()
                    .Where(c => c.ContributionCycleId == current.Id)
                    .Select(c => new { c.UserId, c.Amount })
                    .ToListAsync(ct);

                summary = new StokvelCycleSummary(
                    current.Id,
                    current.Label,
                    current.ClosedAtUtc is null ? "Open" : "Closed",
                    current.EndDate,
                    paid.Select(p => p.UserId).Distinct().Count(),
                    ToCents(paid.Sum(p => p.Amount)));
            }

            return Results.Ok(new StokvelDetailResponse(
                stokvel.Id, stokvel.Name, ToCents(stokvel.ContributionAmount),
                role.Value.ToString(), memberCount, summary));
        })
        .RequireAuthorization()
        .WithTags("Stokvels")
        .WithSummary("Get one stokvel (members only)")
        .WithDescription("""
            Returns the stokvel, the caller's role in it, the member count and a summary of the current cycle.
            404 for a non-member, an unknown id and a malformed id alike, so the answer never confirms
            that a stokvel exists.
            """)
        .Produces<StokvelDetailResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
