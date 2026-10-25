using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Paging;
using RondiTrack.Persistence.Entities;
using RondiTrack.Services;

namespace RondiTrack.Endpoints;

public record MemberListItem(Guid UserId, DateTime JoinedAtUtc, int Role, string FullName);

public static class PagedListEndpoints
{
    private static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public static void MapPagedListEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Stokvels + "/{stokvelId:guid}/contributions",
            async (Guid stokvelId, [AsParameters] MyContributionListQuery query, HttpContext http,
                   IStokvelRepository stokvels, RondiTrackDbContext db, CancellationToken ct) =>
            {
                PageTokenCodec.ClampSize(query.PageSize);

                var userId = http.User.GetUserId();
                if (userId is null)
                    return AuthProblems.Unauthorized(http);

                _ = await stokvels.GetByIdAsync(stokvelId)
                    ?? throw new NotFoundException("Stokvel not found.");

                var page = await MyContributionQuery.ListAsync(db, stokvelId, userId.Value, query, ct);
                return Results.Ok(page);
            })
            .WithSummary("The signed-in user's own contributions in one stokvel")
            .WithDescription("""
                Newest first, paged as { items, nextPageToken } (an empty nextPageToken means no more results).
                Only the caller's own contributions are returned, so no other member's amount or id is exposed.
                Money is integer cents; recordedAt is a UTC instant; status is Pending, Paid or Confirmed.
                """)
            .Produces<PagedResult<ContributionItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policies.StokvelMember);

        app.MapGet(ApiRoutes.Stokvels + "/{stokvelId:guid}/cycles",
            async (Guid stokvelId, IStokvelRepository stokvels, RondiTrackDbContext db, CancellationToken ct) =>
            {
                _ = await stokvels.GetByIdAsync(stokvelId)
                    ?? throw new NotFoundException("Stokvel not found.");

                var cycles = await db.ContributionCycles.AsNoTracking()
                    .Where(c => c.StokvelId == stokvelId)
                    .Select(c => new { c.Id, c.Label, c.Status, c.ClosedAtUtc, c.EndDate, c.TargetAmount })
                    .ToListAsync(ct);

                // The API adds up the money so the browser never has to.
                var collected = await db.Contributions.AsNoTracking()
                    .Where(c => c.StokvelId == stokvelId)
                    .GroupBy(c => c.ContributionCycleId)
                    .Select(g => new { Id = g.Key, Total = g.Sum(c => c.Amount) })
                    .ToDictionaryAsync(x => x.Id, x => x.Total, ct);

                var items = cycles
                    .OrderByDescending(c => c.Label, StringComparer.Ordinal)   // newest first ("2026-10" > "2026-09")
                    .Select(c => new CycleSummaryResponse(
                        c.Id,
                        c.Label,
                        c.Status == CycleStatus.PayoutProcessed ? "PaidOut"
                            : c.ClosedAtUtc is null ? "Open" : "Closed",
                        c.EndDate,
                        ToCents(collected.GetValueOrDefault(c.Id)),
                        ToCents(c.TargetAmount)))
                    .ToList();

                return Results.Ok(items);
            })
            .WithSummary("Every cycle of one stokvel, newest first")
            .WithDescription("""
                Status is Open, Closed (closed by the cycle-close job) or PaidOut. dueDate is the cycle's end date
                as a calendar date (null for undated cycles). collectedCents is everything recorded against the
                cycle; amountDueCents is the cycle's target amount. Money is integer cents.
                """)
            .Produces<List<CycleSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policies.StokvelMember);

        app.MapGet(ApiRoutes.Stokvels + "/{stokvelId:guid}/cycles/{cycleId:guid}/contributions",
            async (Guid stokvelId, Guid cycleId, [AsParameters] ContributionListQuery query,
                   IStokvelRepository stokvels, IContributionCycleRepository cycles,
                   ContributionQueryService service, CancellationToken ct) =>
            {
                PageTokenCodec.ClampSize(query.PageSize);   // reject bad paging input before any lookup

                _ = await stokvels.GetByIdAsync(stokvelId)
                    ?? throw new NotFoundException("Stokvel not found.");
                var cycle = await cycles.GetByIdAsync(cycleId)
                    ?? throw new NotFoundException("Contribution cycle not found.");
                if (cycle.StokvelId != stokvelId)
                    throw new NotFoundException("That contribution cycle does not belong to this stokvel.");

                var page = await service.ListAsync(stokvelId, cycleId, query, ct);
                return Results.Ok(new PagedResult<ContributionResponse>(
                    page.Items.Select(c => c.ToResponse()).ToList(), page.NextPageToken));
            })
            .RequireAuthorization(Policies.StokvelMember);

        app.MapGet(ApiRoutes.Stokvels + "/{stokvelId:guid}/members",
            async (Guid stokvelId, [AsParameters] MemberListQuery query,
                   IStokvelRepository stokvels, MemberQueryService service, CancellationToken ct) =>
            {
                PageTokenCodec.ClampSize(query.PageSize);

                _ = await stokvels.GetByIdAsync(stokvelId)
                    ?? throw new NotFoundException("Stokvel not found.");

                var page = await service.ListAsync(stokvelId, query, ct);
                return Results.Ok(new PagedResult<MemberListItem>(
                    page.Items.Select(m => new MemberListItem(m.UserId, m.JoinedAtUtc, (int)m.Role, m.User?.FullName ?? "")).ToList(),
                    page.NextPageToken));
            })
            .WithSummary("The members of one stokvel, with their names")
            .WithDescription("""
                Treasurer or Admin of the stokvel only, because it lists other members' names. This is what the
                frontend's member picker uses when a Treasurer records a contribution on someone's behalf.
                Paged as { items, nextPageToken } (an empty nextPageToken means no more results).
                """)
            .Produces<PagedResult<MemberListItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policies.StokvelTreasurer);
    }
}
