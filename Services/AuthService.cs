using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;

namespace RondiTrack.Services;

// Lives in Services, not Dtos, on purpose: it holds the raw refresh token, which may only ever
// be written to the cookie and must never be serialised into a response body.
public record AuthResult(LoginResponse Response, string RefreshToken, DateTime RefreshExpiresAtUtc);

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> RefreshAsync(string? presentedToken);
}

public class AuthService(
    IUserRepository users,
    IUserCredentialStore credentials,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IOptions<JwtOptions> jwt) : IAuthService
{
    // Hashed once, so an unknown email costs about the same time as a wrong password.
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

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await credentials.FindByEmailAsync(User.NormalizeEmail(request.Email));

        // Always verify against *some* hash, so every failure path does the same work.
        var result = hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash.Value, request.Password);

        // Unknown email, no password set, inactive account and wrong password all look identical.
        if (user is null || user.PasswordHash is null || !user.IsActive || result == PasswordVerificationResult.Failed)
            throw new InvalidCredentialsException();

        return await IssueAsync(user, familyId: Guid.NewGuid());   // a new login starts a new family
    }

    public async Task<AuthResult> RefreshAsync(string? presentedToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            throw new InvalidSessionException();

        var now = DateTime.UtcNow;
        var stored = await refreshTokens.FindByHashAsync(RefreshTokenGenerator.Hash(presentedToken));

        if (stored is null || stored.IsRevoked || stored.IsExpired(now))
            throw new InvalidSessionException();

        // Claim the token. Only one caller can win this, even if two requests arrive at the same instant.
        // A token that was already used fails here too (reuse detection is added in the next step).
        if (!await refreshTokens.TryMarkUsedAsync(stored.Id, now))
            throw new InvalidSessionException();

        var user = await credentials.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive)
            throw new InvalidSessionException();

        return await IssueAsync(user, stored.FamilyId);   // same family: this is a descendant of the same login
    }

    private async Task<AuthResult> IssueAsync(User user, Guid familyId)
    {
        var now = DateTime.UtcNow;
        var raw = RefreshTokenGenerator.Generate();
        var entity = new RefreshToken(
            user.Id, familyId, RefreshTokenGenerator.Hash(raw), now, TimeSpan.FromDays(jwt.Value.RefreshTokenDays));
        await refreshTokens.AddAsync(entity);

        var access = tokens.CreateAccessToken(user);
        return new AuthResult(new LoginResponse(access.Value, access.ExpiresInSeconds), raw, entity.ExpiresAtUtc);
    }
}