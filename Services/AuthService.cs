using Microsoft.AspNetCore.Identity;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;

namespace RondiTrack.Services;

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request);
}

public class AuthService(
    IUserRepository users,
    IUserCredentialStore credentials,
    IPasswordHasher<User> hasher) : IAuthService
{
    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        var user = new User(request.FullName, request.Email);   // normalises the email

        if (await credentials.EmailExistsAsync(user.Email))
            throw new ConflictException("An account with this email already exists.");

        user.SetPasswordHash(hasher.HashPassword(user, request.Password));
        await users.AddAsync(user);
        return user.ToResponse();
    }
}