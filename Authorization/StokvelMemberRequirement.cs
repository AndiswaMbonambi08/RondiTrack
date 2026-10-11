using Microsoft.AspNetCore.Authorization;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Authorization;

public sealed class StokvelMemberRequirement(StokvelMemberRole minimumRole) : IAuthorizationRequirement
{
    public StokvelMemberRole MinimumRole { get; } = minimumRole;
}