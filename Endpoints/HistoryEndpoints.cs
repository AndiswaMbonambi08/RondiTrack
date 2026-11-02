using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Paging;
using RondiTrack.Services;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        app.MapGet(ApiRoutes.Stokvels + "/{stokvelId:guid}/contribution-history",
            async (Guid stokvelId, [AsParameters] HistoryQuery query, HttpContext http,
                   IStokvelMembershipLookup membership, RondiTrackDbContext db, CancellationToken ct) =>
            {
                var callerId = http.User.GetUserId();
                if (callerId is null)
                    return AuthProblems.Unauthorized(http);

                var parsed = ContributionHistoryQuery.Parse(query);   // 400 for anything malformed

                if (!await db.Stokvels.AsNoTracking().AnyAsync(s => s.Id == stokvelId, ct))
                    throw new NotFoundException("Stokvel not found.");

                var role = await membership.GetRoleAsync(stokvelId, callerId.Value, ct);
                var privileged = http.User.IsInRole(PlatformRoles.PlatformAdmin) || role >= StokvelMemberRole.Treasurer;

                // A plain Member only ever sees their own contributions. Asking for somebody else is a 403.
                var member = parsed.Member;
                if (!privileged)
                {
                    if (member is not null && member != callerId)
                        return AuthProblems.Forbidden(http);
                    member = callerId;
                }

                var page = await ContributionHistoryQuery.ListAsync(db, stokvelId, member, parsed, query, ct);
                return Results.Ok(page);
            })
            .RequireAuthorization(Policies.StokvelMember)
            .WithTags("Stokvels")
            .WithSummary("A stokvel's contributions, filtered and paged")
            .WithDescription("""
                Members see only their own contributions (asking for another member is 403). Treasurers and Admins
                see everyone's. Filters: member (a member id; an id that is not in this stokvel gives an empty list),
                from and to (UTC instants such as 2026-09-30T22:00:00Z; from is included, to is excluded; a date
                without a time is 400) and sort (newest or oldest). Paged as { items, nextPageToken }. A pageToken
                only works with the filters it came from; with different filters the answer is 400.
                """)
            .Produces<PagedResult<HistoryItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet(ApiRoutes.Me + "/activity",
            async (HttpContext http, [AsParameters] ActivityQuery query, RondiTrackDbContext db, CancellationToken ct) =>
            {
                var callerId = http.User.GetUserId();
                if (callerId is null)
                    return AuthProblems.Unauthorized(http);

                var page = await ActivityFeedQuery.ListAsync(db, callerId.Value, query, ct);
                return Results.Ok(page);
            })
            .RequireAuthorization()
            .WithTags("Me")
            .WithSummary("Recent activity across the caller's stokvels")
            .WithDescription("""
                Contributions, payouts and members joining, across the stokvels the caller belongs to, newest first,
                paged as { items, nextPageToken }. A Member sees only their own contributions; a Treasurer or Admin
                sees everyone's in their stokvels. Names of other people are shown only to Treasurers and Admins.
                """)
            .Produces<PagedResult<ActivityItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}