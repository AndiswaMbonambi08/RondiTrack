// StokvelMember used to be a thin join row with a few public-settable fields.
// It now carries real data of its own — Role and JoinedAtUtc — which is exactly
// why it gets its own composite primary key on (StokvelId, UserId) instead of a
// synthetic Guid Id: nobody will ever look this row up by an arbitrary id, they
// look it up by "this user's membership in this stokvel," which the pair of
// foreign keys already expresses completely. A surrogate id here would just be
// an extra column with no meaning attached to it.
namespace RondiTrack.Persistence.Entities;

public enum StokvelMemberRole
{
    Member,
    Treasurer,
    Admin
}

public class StokvelMember
{
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public StokvelMemberRole Role { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }

    // Single-reference navigations only — StokvelMember doesn't need to expose
    // a collection back to Stokvel or User, since nothing queries "give me this
    // stokvel's StokvelMembers" through the Stokvel object itself; that's what
    // IStokvelMemberRepository (Phase 3) is for.
    public RondiTrack.Domain.Stokvel? Stokvel { get; private set; }
    public RondiTrack.Domain.User? User { get; private set; }

    public ICollection<RondiTrack.Domain.Contribution> Contributions { get; private set; } = new List<RondiTrack.Domain.Contribution>();

    private StokvelMember() { } // EF materialization only

    public StokvelMember(Guid stokvelId, Guid userId, StokvelMemberRole role, DateTime joinedAtUtc)
    {
        StokvelId = stokvelId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }
}