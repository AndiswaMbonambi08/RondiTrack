using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Paging;
using RondiTrack.Persistence.Entities;
using RondiTrack.Services;

namespace RondiTrack.Endpoints;

public record MemberListItem(Guid UserId, DateTime JoinedAtUtc, int Role);

public static class PagedListEndpoints
{
    public static void MapPagedListEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/contributions",
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
            });

        app.MapGet("/api/stokvels/{stokvelId:guid}/members",
            async (Guid stokvelId, [AsParameters] MemberListQuery query,
                   IStokvelRepository stokvels, MemberQueryService service, CancellationToken ct) =>
            {
                PageTokenCodec.ClampSize(query.PageSize);

                _ = await stokvels.GetByIdAsync(stokvelId)
                    ?? throw new NotFoundException("Stokvel not found.");

                var page = await service.ListAsync(stokvelId, query, ct);
                return Results.Ok(new PagedResult<MemberListItem>(
                    page.Items.Select(m => new MemberListItem(m.UserId, m.JoinedAtUtc, (int)m.Role)).ToList(),
                    page.NextPageToken));
            });
    }
}