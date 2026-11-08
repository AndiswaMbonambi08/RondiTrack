using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Endpoints;

public record ProofResponse(Guid ContributionId, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc);

public static class ProofRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    // The file's own bytes decide the type. The name and the browser's Content-Type are never trusted.
    public static string? Detect(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46 && b[4] == 0x2D) return "application/pdf";
        return null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        _ => ".pdf"
    };
}

public static class ProofEndpoints
{
    private const string NotFoundText = "Proof of payment not found.";

    public static void MapProofEndpoints(this WebApplication app)
    {
        var route = ApiRoutes.Stokvels + "/{stokvelId:guid}/contributions/{contributionId:guid}/proof";

        app.MapPost(route, async (
            Guid stokvelId, Guid contributionId, IFormFile? file, HttpContext http,
            IStokvelMembershipLookup membership, RondiTrackDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);

            var gate = await GateAsync(http, db, membership, stokvelId, contributionId, callerId.Value, ct);
            if (gate.Denied is not null) return gate.Denied;

            if (file is null || file.Length == 0)
                return CodedProblems.Make(http, 400, "No file", "Choose a file to upload.", "file-missing");
            if (file.Length > ProofRules.MaxBytes)
                return CodedProblems.Make(http, 413, "File too large", "The file is larger than 5 MB.", "file-too-large");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var type = ProofRules.Detect(bytes);
            if (type is null)
                return CodedProblems.Make(http, 400, "File type not accepted",
                    "Only JPEG, PNG or PDF files are accepted. The file's contents are checked, not its name.", "file-type");

            var now = time.GetUtcNow().UtcDateTime;
            var name = "proof-of-payment" + ProofRules.Extension(type);
            var existing = await db.ProofsOfPayment.FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct);
            if (existing is null)
            {
                existing = new ProofOfPayment(contributionId, stokvelId, callerId.Value, name, type, bytes, now);
                db.ProofsOfPayment.Add(existing);
            }
            else
            {
                existing.Replace(callerId.Value, name, type, bytes, now);
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ProofResponse(contributionId, existing.FileName, existing.ContentType, existing.SizeBytes, existing.UploadedAtUtc));
        })
        .DisableAntiforgery() // a bearer-token API: there is no cookie for a forged form to ride on
        .WithTags("ProofOfPayment")
        .WithSummary("Attach (or replace) a proof of payment")
        .WithDescription("The contribution's owner or a Treasurer/Admin of the stokvel. JPEG, PNG or PDF, up to 5 MB, judged by the file's bytes. 409 once the cycle is closed. Anyone else gets 404.")
        .Produces<ProofResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        app.MapDelete(route, async (
            Guid stokvelId, Guid contributionId, HttpContext http,
            IStokvelMembershipLookup membership, RondiTrackDbContext db, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);

            var gate = await GateAsync(http, db, membership, stokvelId, contributionId, callerId.Value, ct);
            if (gate.Denied is not null) return gate.Denied;

            await db.ProofsOfPayment.Where(p => p.ContributionId == contributionId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        })
        .WithTags("ProofOfPayment")
        .WithSummary("Remove a proof of payment (cycle still open)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet(route, async (
            Guid stokvelId, Guid contributionId, HttpContext http,
            IStokvelMembershipLookup membership, RondiTrackDbContext db, CancellationToken ct) =>
        {
            var callerId = http.User.GetUserId();
            if (callerId is null) return AuthProblems.Unauthorized(http);

            var proof = await db.ProofsOfPayment.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ContributionId == contributionId && p.StokvelId == stokvelId, ct);
            var role = await membership.GetRoleAsync(stokvelId, callerId.Value, ct);

            // The uploader, or a Treasurer/Admin of THIS stokvel. Everyone else gets the same 404 as a missing file.
            var allowed = proof is not null && role is not null
                          && (proof.UploadedByUserId == callerId.Value || role >= StokvelMemberRole.Treasurer);
            if (!allowed) return AuthProblems.NotFound(http, NotFoundText);

            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            http.Response.Headers["Cache-Control"] = "private, no-store";
            return Results.File(proof!.Content, proof.ContentType, proof.FileName);
        })
        .WithTags("ProofOfPayment")
        .WithSummary("Download a proof of payment")
        .WithDescription("Only the uploader and Treasurers/Admins of the stokvel the contribution belongs to. Everyone else: 404.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // Shared by upload and remove: owner or treasurer of this stokvel (else 404), and the cycle must be open (else 409).
    private static async Task<(IResult? Denied, Guid CycleId)> GateAsync(
        HttpContext http, RondiTrackDbContext db, IStokvelMembershipLookup membership,
        Guid stokvelId, Guid contributionId, Guid callerId, CancellationToken ct)
    {
        var contribution = await db.Contributions.AsNoTracking()
            .Where(c => c.Id == contributionId && c.StokvelId == stokvelId)
            .Select(c => new { c.UserId, c.ContributionCycleId })
            .FirstOrDefaultAsync(ct);
        var role = await membership.GetRoleAsync(stokvelId, callerId, ct);

        if (contribution is null || role is null ||
            (contribution.UserId != callerId && role < StokvelMemberRole.Treasurer))
            return (AuthProblems.NotFound(http, NotFoundText), Guid.Empty);

        var closed = await db.ContributionCycles.AsNoTracking()
            .AnyAsync(c => c.Id == contribution.ContributionCycleId && c.ClosedAtUtc != null, ct);
        if (closed)
            return (CodedProblems.Make(http, 409, "Proof is locked",
                "This cycle is closed, so its proofs of payment can no longer be changed.", "proof-locked"), Guid.Empty);

        return (null, contribution.ContributionCycleId);
    }
}