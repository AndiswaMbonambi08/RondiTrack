using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Jobs;
using StokvelMember = RondiTrack.Persistence.Entities.StokvelMember;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Endpoints;

public record CreateInviteRequest(int? ExpiresInDays);
public record CreateInviteResponse(string Code, DateTime ExpiresAtUtc);
public record SetRulesRequest(string? Rules);
public record JoinResponse(Guid StokvelId);

// Everything a person needs to decide, and nothing about the members: only a count.
// StokvelId is filled in only when the caller is already a member.
public record InvitePreviewResponse(
    string StokvelName, string Rules, long ContributionCents, string Schedule,
    int CycleEndDay, int MemberCount, bool AlreadyMember, Guid? StokvelId);

public static class JoinEndpoints
{
    private static readonly Regex CodeShape = new("^[A-Z0-9]{6,20}$", RegexOptions.Compiled);
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static void MapJoinEndpoints(this WebApplication app)
    {
        app.MapPost(ApiRoutes.Stokvels + "/{stokvelId:guid}/invites", async (
            Guid stokvelId, CreateInviteRequest? request, HttpContext http,
            RondiTrackDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);

            var days = request?.ExpiresInDays ?? 7;
            if (days is < 1 or > 30) throw new RequestValidationException("expiresInDays must be between 1 and 30.");

            var now = time.GetUtcNow().UtcDateTime;
            var code = new string(RandomNumberGenerator.GetItems(CodeAlphabet.AsSpan(), 10));
            var invite = new StokvelInvite(stokvelId, code, callerId.Value, now, now.AddDays(days));
            db.StokvelInvites.Add(invite);
            await db.SaveChangesAsync(ct);
            return Results.Created($"{ApiRoutes.Base}/invites/{code}", new CreateInviteResponse(code, invite.ExpiresAtUtc));
        })
        .RequireAuthorization(Policies.StokvelTreasurer)
        .WithTags("Invites")
        .WithSummary("Create an invite code for a stokvel")
        .WithDescription("Treasurer or Admin of the stokvel only. expiresInDays is 1 to 30 (default 7).")
        .Produces<CreateInviteResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPut(ApiRoutes.Stokvels + "/{stokvelId:guid}/rules", async (
            Guid stokvelId, SetRulesRequest request, RondiTrackDbContext db, CancellationToken ct) =>
        {
            var rules = (request.Rules ?? "").Trim();
            if (rules.Length > 2000) throw new RequestValidationException("rules must be at most 2000 characters.");

            // ExecuteUpdate: the Stokvel is never loaded, so the membership reconcile never sees it.
            var changed = await db.Stokvels.Where(s => s.Id == stokvelId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => EF.Property<string>(x, "Rules"), rules), ct);
            if (changed == 0) throw new NotFoundException("Stokvel not found.");
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.StokvelTreasurer)
        .WithTags("Invites")
        .WithSummary("Set the rules shown to people previewing an invite")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet(ApiRoutes.Base + "/invites/{code}", async (
            string code, HttpContext http, IStokvelMembershipLookup membership, RondiTrackDbContext db,
            IOptions<CycleCloseOptions> options, TimeProvider time, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);

            var (invite, problem) = await FindAsync(http, db, time, code, ct);
            if (problem is not null) return problem;

            var stokvel = await db.Stokvels.AsNoTracking()
                .Where(s => s.Id == invite!.StokvelId)
                .Select(s => new { s.Name, s.ContributionAmount, s.Rules })
                .FirstOrDefaultAsync(ct);
            if (stokvel is null) return Unknown(http);

            var count = await db.StokvelMembers.AsNoTracking().CountAsync(m => m.StokvelId == invite!.StokvelId, ct);
            var role = await membership.GetRoleAsync(invite!.StokvelId, callerId.Value, ct);
            var already = role is not null;
            var endDay = options.Value.CycleEndDay;

            return Results.Ok(new InvitePreviewResponse(
                stokvel.Name, stokvel.Rules,
                (long)decimal.Round(stokvel.ContributionAmount * 100m, MidpointRounding.AwayFromZero),
                $"Contributions are due by day {endDay} of each month (South African time).",
                endDay, count, already, already ? invite.StokvelId : null));
        })
        .WithTags("Invites")
        .WithSummary("Preview an invite (any signed-in person)")
        .WithDescription("Returns the stokvel's name, rules, contribution amount, schedule and member COUNT. No names, balances or payout order. 404 unknown code, 410 expired code.")
        .Produces<InvitePreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status410Gone);

        app.MapPost(ApiRoutes.Base + "/invites/{code}/join", async (
            string code, HttpContext http, RondiTrackDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);

            var (invite, problem) = await FindAsync(http, db, time, code, ct);
            if (problem is not null) return problem;
            var stokvelId = invite!.StokvelId;

            var isActive = await db.Users.AsNoTracking()
                .Where(u => u.Id == callerId.Value)
                .Select(u => (bool?)u.IsActive)
                .FirstOrDefaultAsync(ct);
            if (isActive is null) throw new NotFoundException("User not found.");
            if (isActive == false)
                return CodedProblems.Make(http, 409, "Cannot join", "This account is inactive.", "account-inactive");

            var exists = await db.StokvelMembers.AsNoTracking()
                .AnyAsync(m => m.StokvelId == stokvelId && m.UserId == callerId.Value, ct);
            if (exists) return AlreadyMember(http, stokvelId);

            db.StokvelMembers.Add(new StokvelMember(
                stokvelId, callerId.Value, StokvelMemberRole.Member, time.GetUtcNow().UtcDateTime));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // A double click: the other request won the composite key. One membership, same answer.
                return AlreadyMember(http, stokvelId);
            }
            return Results.Created($"{ApiRoutes.Stokvels}/{stokvelId}", new JoinResponse(stokvelId));
        })
        .WithTags("Invites")
        .WithSummary("Join a stokvel with an invite code")
        .WithDescription("Joins as a Member. 409 (code already-member, with stokvelId) if the caller is already in. Repeating the request never makes a second membership.")
        .Produces<JoinResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status410Gone);
    }

    private static IResult Unknown(HttpContext http) =>
        CodedProblems.Make(http, 404, "Invite not found", "Check the code and try again.", "invite-unknown");

    private static IResult AlreadyMember(HttpContext http, Guid stokvelId) =>
        CodedProblems.Make(http, 409, "Already a member", "You are already a member of this stokvel.",
            "already-member", new Dictionary<string, object?> { ["stokvelId"] = stokvelId });

    private static async Task<(StokvelInvite? Invite, IResult? Problem)> FindAsync(
        HttpContext http, RondiTrackDbContext db, TimeProvider time, string rawCode, CancellationToken ct)
    {
        var code = rawCode.Trim().ToUpperInvariant();
        if (!CodeShape.IsMatch(code)) return (null, Unknown(http));

        var invite = await db.StokvelInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Code == code && i.RevokedAtUtc == null, ct);
        if (invite is null) return (null, Unknown(http));

        if (invite.ExpiresAtUtc <= time.GetUtcNow().UtcDateTime)
            return (null, CodedProblems.Make(http, 410, "Invite expired", "Ask for a new invite.", "invite-expired"));

        return (invite, null);
    }
}