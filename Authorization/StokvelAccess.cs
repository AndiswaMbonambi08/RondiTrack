using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Authorization;

// Runs a stokvel policy for routes where the stokvel id is not in the URL. Returns null when
// allowed, otherwise the problem response: 404 for a non-member, 403 for a member without the role.
public sealed class StokvelAccess(IAuthorizationService authorization)
{
    public async Task<IResult?> DenyAsync(HttpContext http, Guid stokvelId, string policy, string? notFoundDetail = null)
    {
        var result = await authorization.AuthorizeAsync(http.User, new StokvelResource(stokvelId), policy);
        if (result.Succeeded) return null;

        return AuthProblems.IsNotMember(result.Failure)
            ? AuthProblems.NotFound(http, notFoundDetail)
            : AuthProblems.Forbidden(http);
    }
}