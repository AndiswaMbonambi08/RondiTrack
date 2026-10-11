using System.Text;

namespace RondiTrack.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int ClockSkewSeconds { get; set; } = 30;

    // Called at startup so a missing or weak key fails immediately with a clear message,
    // instead of surfacing later as a confusing error on the first login.
    public static void EnsureValid(JwtOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.SigningKey))
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it with: dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 bytes>\" --project RondiTrack.csproj " +
                "(or the environment variable Jwt__SigningKey). It must never go in appsettings.json.");

        if (Encoding.UTF8.GetByteCount(o.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes long.");

        if (string.IsNullOrWhiteSpace(o.Issuer) || string.IsNullOrWhiteSpace(o.Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured in appsettings.json.");

        if (o.AccessTokenMinutes is < 1 or > 15)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 15.");
    }
}