// GET /api/v1/stokvels/{id}/cycles/{cycleId}/payout-order: who is paid, who is next, who is waiting.
// A non-member, an unknown stokvel, an unknown cycle, a cycle of another stokvel and a malformed id
// all get the same 404, so the answer never confirms that a stokvel or cycle exists.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Browser;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;

namespace RondiTrack.Endpoints;

public static class PayoutOrderEndpoints
{
    private const string NotFoundText = "Payout order not found.";
    private static readonly TimeZoneInfo Johannesburg = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    public static void MapPayoutOrderEndpoints(this WebApplication app)
    {
        app.MapGet(ApiRoutes.Stokvels + "/{id}/cycles/{cycleId}/payout-order", async (
            string id,
            string cycleId,
            HttpContext http,
            IStokvelMembershipLookup membership,
            RondiTrackDbContext db,
            CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null)
                return AuthProblems.Forbidden(http);

            if (!Guid.TryParse(id, out var stokvelId) || !Guid.TryParse(cycleId, out var cycleGuid))
                throw new NotFoundException(NotFoundText);

            var role = await membership.GetRoleAsync(stokvelId, callerId.Value, ct);
            if (role is null)
                throw new NotFoundException(NotFoundText);

            var cycleInStokvel = await db.ContributionCycles.AsNoTracking()
                .AnyAsync(c => c.Id == cycleGuid && c.StokvelId == stokvelId, ct);
            if (!cycleInStokvel)
                throw new NotFoundException(NotFoundText);

            var members = await db.StokvelMembers.AsNoTracking()
                .Where(m => m.StokvelId == stokvelId)
                .Select(m => new PayoutOrderMember(m.UserId, m.User!.FullName, m.JoinedAtUtc))
                .ToListAsync(ct);

            var contributionAmount = await db.Stokvels.AsNoTracking()
                .Where(s => s.Id == stokvelId)
                .Select(s => s.ContributionAmount)
                .FirstAsync(ct);

            var payouts = await db.Payouts.AsNoTracking()
                .Where(p => p.StokvelId == stokvelId)
                .Select(p => new { p.RecipientUserId, p.PayoutDate })
                .ToListAsync(ct);

            var paidAt = payouts
                .GroupBy(p => p.RecipientUserId)
                .ToDictionary(g => g.Key, g => g.Min(p => p.PayoutDate));

            var potCents = ToCents(contributionAmount) * members.Count;
            var lines = PayoutOrderBuilder.Build(members, paidAt, potCents);

            return Results.Ok(new PayoutOrderResponse(
                stokvelId,
                cycleGuid,
                lines.Select(l => new PayoutOrderEntryResponse(
                    l.UserId,
                    l.FullName,
                    l.Position,
                    l.AmountCents,
                    l.PaidAtUtc is null ? null : ToJohannesburgDate(l.PaidAtUtc.Value),
                    l.Status.ToString())).ToList()));
        })
        .RequireAuthorization()
        .WithTags("Payouts")
        .WithSummary("Get a cycle's payout order (members only)")
        .WithDescription("""
            Returns every member in rotation order with their position, the pot in cents, the payout date
            (null until paid) and a status of Paid, Next or Waiting. The server decides the order and who is Next.
            404 for a non-member, an unknown stokvel or cycle, a cycle of another stokvel and a malformed id alike.
            """)
        .Produces<PayoutOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static DateOnly ToJohannesburgDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Johannesburg));
}