// Hand-written mapping from the User entity to its response DTO. One place, one direction.
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Mapping;

public static class UserMapping
{
    public static UserResponse ToResponse(this User user)
        => new(user.Id, user.FullName, user.Email, user.IsActive);
}