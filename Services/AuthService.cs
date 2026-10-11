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
    Task LogoutAsync(string? presentedToken);
}

public class AuthService(
    IUserRepository users,
    IUserCredentialStore credentials,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IOptions<JwtOptions> jwt,
    ILogger<AuthService> logger) : IAuthService
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

        var familyId = Guid.NewGuid();   // a new login starts a new family
        var issued = await IssueAsync(user, familyId);

        // Ids only. Never log a password, token, hash or email.
        logger.LogInformation("Login succeeded. UserId: {UserId}, FamilyId: {FamilyId}", user.Id, familyId);
        return issued;
    }

    public async Task<AuthResult> RefreshAsync(string? presentedToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            throw new InvalidSessionException();

        var now = DateTime.UtcNow;
        var stored = await refreshTokens.FindByHashAsync(RefreshTokenGenerator.Hash(presentedToken));

        if (stored is null)
            throw new InvalidSessionException();

        // This token was already exchanged. The real client holds the newer one, so this is a replay.
        // Kill every token from this login, including the newest one.
        if (stored.IsUsed)
        {
            await RevokeFamilyAsync(stored, now);
            throw new InvalidSessionException();
        }

        if (stored.IsRevoked || stored.IsExpired(now))
            throw new InvalidSessionException();

        // Claim the token. Only one caller can win, even if two requests arrive at the same instant.
        // The loser can't be told apart from a replay, so it's treated as one.
        if (!await refreshTokens.TryMarkUsedAsync(stored.Id, now))
        {
            await RevokeFamilyAsync(stored, now);
            throw new InvalidSessionException();
        }

        var user = await credentials.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive)
            throw new InvalidSessionException();

        var issued = await IssueAsync(user, stored.FamilyId);   // same family: a descendant of the same login

        // Ids only. Never log a token or its hash.
        logger.LogInformation("Refresh token rotated. UserId: {UserId}, FamilyId: {FamilyId}", user.Id, stored.FamilyId);
        return issued;
    }

    private async Task RevokeFamilyAsync(RefreshToken token, DateTime now)
    {
        await refreshTokens.RevokeFamilyAsync(token.FamilyId, now);

        // Ids only. Never log a token or its hash.
        logger.LogWarning(
            "Refresh token reuse detected; session family revoked. UserId: {UserId}, FamilyId: {FamilyId}",
            token.UserId, token.FamilyId);
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

    // Idempotent: no cookie, an unknown token and an already-revoked family all end the same way.
    public async Task LogoutAsync(string? presentedToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            return;

        var stored = await refreshTokens.FindByHashAsync(RefreshTokenGenerator.Hash(presentedToken));
        if (stored is not null)
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, DateTime.UtcNow);
    }
}