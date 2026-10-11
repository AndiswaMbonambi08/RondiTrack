using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Authorization;

// Used when a route does not carry the stokvel id but the code has looked it up (payouts, cycles).
public sealed record StokvelResource(Guid StokvelId);

public sealed class StokvelMemberHandler(IHttpContextAccessor accessor, IStokvelMembershipLookup membership)
    : AuthorizationHandler<StokvelMemberRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StokvelMemberRequirement requirement)
    {
        var userId = context.User.GetUserId();
        if (userId is null) return; // unauthenticated: the policy challenges

        if (context.User.IsInRole(PlatformRoles.PlatformAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var stokvelId = ResolveStokvelId(context);
        if (stokvelId is null)
        {
            context.Fail(new AuthorizationFailureReason(this, StokvelAuthFailures.NotMember));
            return;
        }

        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var role = await membership.GetRoleAsync(stokvelId.Value, userId.Value, ct);

        if (role is null)
            context.Fail(new AuthorizationFailureReason(this, StokvelAuthFailures.NotMember));
        else if (role >= requirement.MinimumRole)
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, StokvelAuthFailures.InsufficientRole));
    }

    // A stokvel passed in as the resource wins. Otherwise read the route, where the parameter is
    // {stokvelId} in some places and {id} in others.
    private Guid? ResolveStokvelId(AuthorizationHandlerContext context)
    {
        if (context.Resource is StokvelResource resource)
            return resource.StokvelId;

        var values = accessor.HttpContext?.Request.RouteValues;
        var raw = (values?["stokvelId"] ?? values?["id"])?.ToString();
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}