using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RondiTrack.Domain;

namespace RondiTrack.Services;

public record AccessToken(string Value, int ExpiresInSeconds);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}

// The token carries who the caller is (sub), a unique id (jti) and, only for platform admins, a
// platform-level role. Nothing about stokvels, per-stokvel roles or email: those can change within
// the token's lifetime and are checked against the database. The platform admin flag is the one
// fact that is acceptable to be up to 15 minutes stale.
public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var lifetime = TimeSpan.FromMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.IsPlatformAdmin)
            claims.Add(new Claim("role", "PlatformAdmin"));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }
}