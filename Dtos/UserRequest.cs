// What a caller sends to create or update a User. Kept separate from the User entity
// so a request body can never smuggle in fields like IsActive (over-posting).
namespace RondiTrack.Dtos;

public record UserRequest(string FullName, string Email);