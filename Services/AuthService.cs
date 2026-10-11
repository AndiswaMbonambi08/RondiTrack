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
    Task<LoginResponse> LoginAsync(LoginRequest request);
}

public class AuthService(
    IUserRepository users,
    IUserCredentialStore credentials,
    IPasswordHasher<User> hasher,
    ITokenService tokens) : IAuthService
{
    // Hashed once, so an unknown email costs about the same time as a wrong password.
    // Otherwise response time would reveal which emails have accounts.
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<User>().HashPassword(null!, "not-a-real-password"));

    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        var user = new User(request.FullName, request.Email);   // normalises the email

        if (await credentials.EmailExistsAsync(user.Email))
            throw new ConflictException("An account with this email already exists.");

        user.SetPasswordHash(hasher.HashPassword(user, request.Password));
        await users.AddAsync(user);
        return user.ToResponse();
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await credentials.FindByEmailAsync(User.NormalizeEmail(request.Email));

        // Always verify against *some* hash, so every failure path does the same work.
        var result = hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash.Value, request.Password);

        // Unknown email, no password set, inactive account and wrong password all look identical.
        if (user is null || user.PasswordHash is null || !user.IsActive || result == PasswordVerificationResult.Failed)
            throw new InvalidCredentialsException();

        var token = tokens.CreateAccessToken(user);
        return new LoginResponse(token.Value, token.ExpiresInSeconds);
    }
}