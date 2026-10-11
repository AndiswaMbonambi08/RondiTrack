using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Authorization;

public sealed class SelfOrPlatformAdminRequirement : IAuthorizationRequirement;

// A user may act on their own record. Platform admins may act on anyone's. Routes with no {id}
// (list, create) therefore end up admin only.
public sealed class SelfOrPlatformAdminHandler(IHttpContextAccessor accessor)
    : AuthorizationHandler<SelfOrPlatformAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SelfOrPlatformAdminRequirement requirement)
    {
        var userId = context.User.GetUserId();
        if (userId is null) return Task.CompletedTask;

        if (context.User.IsInRole(PlatformRoles.PlatformAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var raw = accessor.HttpContext?.Request.RouteValues["id"]?.ToString();
        if (Guid.TryParse(raw, out var id) && id == userId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}