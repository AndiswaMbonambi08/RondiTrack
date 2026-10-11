using Microsoft.AspNetCore.Authorization;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Authorization;

public sealed record ContributionTarget(Guid StokvelId, Guid TargetUserId);

public sealed class RecordContributionRequirement : IAuthorizationRequirement;

public sealed class RecordContributionHandler(IStokvelMembershipLookup membership)
    : AuthorizationHandler<RecordContributionRequirement, ContributionTarget>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RecordContributionRequirement requirement,
        ContributionTarget target)
    {
        var userId = context.User.GetUserId();
        if (userId is null) return;

        if (context.User.IsInRole(PlatformRoles.PlatformAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var role = await membership.GetRoleAsync(target.StokvelId, userId.Value, CancellationToken.None);
        if (role is null) return;

        // A member may record their own contribution. Treasurer or Admin may record anyone's.
        if (userId == target.TargetUserId || role >= StokvelMemberRole.Treasurer)
            context.Succeed(requirement);
    }
}