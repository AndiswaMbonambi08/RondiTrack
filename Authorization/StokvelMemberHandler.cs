using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Authorization;

public sealed class StokvelMemberHandler(IHttpContextAccessor accessor, IStokvelMembershipLookup membership)
    : AuthorizationHandler<StokvelMemberRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StokvelMemberRequirement requirement)
    {
        var userId = context.User.GetUserId();
        var http = accessor.HttpContext;
        if (userId is null || http is null) return; // unauthenticated: the policy challenges

        if (context.User.IsInRole(PlatformRoles.PlatformAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        // Your routes use {stokvelId} in some places and {id} in others.
        var values = http.Request.RouteValues;
        var raw = (values["stokvelId"] ?? values["id"])?.ToString();
        if (!Guid.TryParse(raw, out var stokvelId))
        {
            context.Fail(new AuthorizationFailureReason(this, StokvelAuthFailures.NotMember));
            return;
        }

        var role = await membership.GetRoleAsync(stokvelId, userId.Value, http.RequestAborted);
        if (role is null)
            context.Fail(new AuthorizationFailureReason(this, StokvelAuthFailures.NotMember));
        else if (role >= requirement.MinimumRole)
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, StokvelAuthFailures.InsufficientRole));
    }
}