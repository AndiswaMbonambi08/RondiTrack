using RondiTrack.Domain.Exceptions;
using RondiTrack.Services;

namespace RondiTrack.Endpoints;

public static class PayoutEndpoints
{
    public static void MapPayoutEndpoints(this WebApplication app)
    {
        app.MapPost("/api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payout", async (
            Guid stokvelId, Guid cycleId, IPayoutService service) =>
        {
            var response = await service.ProcessPayoutAsync(stokvelId, cycleId);
            return Results.Created($"/api/stokvels/{stokvelId}/payouts/{response.Id}", response);
        })
        .WithTags("Payouts")
        .WithSummary("Process the next eligible payout for a cycle")
        .WithDescription("""
            Picks the earliest-joined member who hasn't received a payout from this
            stokvel yet, records the payout, and marks the cycle as processed. Both
            writes happen inside one transaction; fails 404 if either resource does
            not exist, 409 if the cycle is already processed or no eligible member remains.
            """)
        .Produces<RondiTrack.Dtos.PayoutResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}