// What a caller receives back for a User. Mirrors the entity here, but exists as its
// own type so the wire shape can change independently of the domain model.
namespace RondiTrack.Dtos;

public record UserResponse(Guid Id, string FullName, string Email, bool IsActive);