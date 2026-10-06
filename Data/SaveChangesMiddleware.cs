// Runs once per request. Stokvel.AddMember changes an in-memory list, so EF never sees it on its own.
// After the endpoint runs, we reconcile memberships and save BEFORE the response is released to the client.
// The response is buffered so that a database error (unique violation, concurrency conflict) still reaches
// the central exception handler as a proper 409/412 instead of arriving after a success was already sent.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Data;

public class SaveChangesMiddleware
{
    private readonly RequestDelegate _next;

    public SaveChangesMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, RondiTrackDbContext db)
    {
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
            await ReconcileMembershipsAsync(db);
            await db.SaveChangesAsync();
        }
        catch
        {
            context.Response.Body = original;   // nothing was sent yet, so the exception handler can still set the status
            throw;
        }

        context.Response.Body = original;
        buffer.Position = 0;
        await buffer.CopyToAsync(original);
    }

    private static async Task ReconcileMembershipsAsync(RondiTrackDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<Stokvel>().ToList())
        {
            var stokvel = entry.Entity;

            var existingMemberIds = await db.StokvelMembers
                .Where(sm => sm.StokvelId == stokvel.Id)
                .Select(sm => sm.UserId)
                .ToListAsync();

            foreach (var memberId in stokvel.MemberIds)
            {
                if (!existingMemberIds.Contains(memberId))
                    db.StokvelMembers.Add(new StokvelMember(stokvel.Id, memberId, StokvelMemberRole.Member, DateTime.UtcNow));
            }

            var removedIds = existingMemberIds.Except(stokvel.MemberIds).ToList();
            if (removedIds.Count > 0)
            {
                var rowsToRemove = await db.StokvelMembers
                    .Where(sm => sm.StokvelId == stokvel.Id && removedIds.Contains(sm.UserId))
                    .ToListAsync();
                db.StokvelMembers.RemoveRange(rowsToRemove);
            }
        }
    }
}