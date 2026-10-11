using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

namespace RondiTrack.Authorization;

// Per-request note of a role other than Member for a membership that is about to be created.
// SaveChangesMiddleware reads it when it writes the StokvelMember row.
public sealed class PendingMemberRoles
{
    private readonly Dictionary<(Guid StokvelId, Guid UserId), StokvelMemberRole> _roles = new();

    public void Set(Guid stokvelId, Guid userId, StokvelMemberRole role) =>
        _roles[(stokvelId, userId)] = role;

    public StokvelMemberRole RoleFor(Guid stokvelId, Guid userId) =>
        _roles.TryGetValue((stokvelId, userId), out var role) ? role : StokvelMemberRole.Member;
}