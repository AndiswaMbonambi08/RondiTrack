using System.Security.Claims;

namespace RondiTrack.Authorization;

public static class ClaimsExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var id) ? id : null;
}