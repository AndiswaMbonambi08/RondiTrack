using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Mapping;

namespace RondiTrack.Endpoints;

public record UpdatePayoutRequest(decimal Amount);

public static class PayoutVersionEndpoints
{
    public static void MapPayoutVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/payouts/{id:guid}", async (Guid id, RondiTrackDbContext db, HttpResponse res, CancellationToken ct) =>
        {
            var payout = await db.Payouts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new NotFoundException("Payout not found.");
            res.Headers.ETag = $"\"{payout.Version}\"";
            return Results.Ok(payout.ToResponse());      // ADAPT if your mapper has a different name
        });

        app.MapPut("/api/payouts/{id:guid}", async (Guid id, UpdatePayoutRequest body, HttpRequest req,
                                                    HttpResponse res, RondiTrackDbContext db, CancellationToken ct) =>
        {
            if (!req.Headers.TryGetValue("If-Match", out var header))
                throw new PreconditionRequiredException("If-Match header is required.");
            if (!uint.TryParse(header.ToString().Trim('"'), out var version))
                throw new RequestValidationException("If-Match is not a valid version.");

            var payout = await db.Payouts.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new NotFoundException("Payout not found.");

            // Compare against the version the client read, not the one just loaded.
            db.Entry(payout).Property(p => p.Version).OriginalValue = version;
            db.Entry(payout).Property(p => p.Amount).CurrentValue = body.Amount;

            await db.SaveChangesAsync(ct);               // stale version throws DbUpdateConcurrencyException -> 412
            res.Headers.ETag = $"\"{payout.Version}\"";
            return Results.Ok(payout.ToResponse());
        });
    }
}