// Runs once per request, after everything else. Because Stokvel.AddMember
// changes an in-memory list rather than calling any repository method, EF
// never sees that change on its own. Here we check every Stokvel EF loaded
// this request, work out which memberships are new or removed, write those
// rows, and save. This keeps the repository interface completely unchanged.
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Data;

public class SaveChangesMiddleware
{
    private readonly RequestDelegate _next;

    public SaveChangesMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, RondiTrackDbContext db)
    {
        await _next(context);

        foreach (var entry in db.ChangeTracker.Entries<Stokvel>().ToList())
        {
            var stokvel = entry.Entity;

            var existingMemberIds = await db.StokvelMembers
                .Where(sm => sm.StokvelId == stokvel.Id)
                .Select(sm => sm.UserId)
                .ToListAsync();

            // Add any member that's in the in-memory list but not yet in the table.
            foreach (var memberId in stokvel.MemberIds)
            {
                if (!existingMemberIds.Contains(memberId))
                {
                    db.StokvelMembers.Add(new RondiTrack.Persistence.Entities.StokvelMember(
                    stokvel.Id, memberId, RondiTrack.Persistence.Entities.StokvelMemberRole.Member, DateTime.UtcNow));
                       
                }
            }

            // Remove any row in the table that's no longer in the in-memory list.
            var removedIds = existingMemberIds.Except(stokvel.MemberIds).ToList();
            if (removedIds.Count > 0)
            {
                var rowsToRemove = await db.StokvelMembers
                    .Where(sm => sm.StokvelId == stokvel.Id && removedIds.Contains(sm.UserId))
                    .ToListAsync();
                db.StokvelMembers.RemoveRange(rowsToRemove);
            }
        }

        await db.SaveChangesAsync();
    }
}