// The real database table behind membership. Stokvel.MemberIds in the domain
// model is just an in-memory list for the business logic to work with; this
// class is what actually gets saved to and loaded from Postgres.
namespace RondiTrack.Persistence.Entities;

public class StokvelMember
{
    public Guid StokvelId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
}