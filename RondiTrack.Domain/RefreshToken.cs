namespace RondiTrack.Domain;

// One refresh token. Only the SHA-256 hash is stored, never the token itself.
// FamilyId ties together every token descended from one login, so a replayed token
// can take the whole family down.
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTime nowUtc, TimeSpan lifetime)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc.Add(lifetime);
    }

    public bool IsUsed => UsedAtUtc is not null;
    public bool IsRevoked => RevokedAtUtc is not null;
    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;
}