namespace RondiTrack.Domain;

// An invite code a treasurer hands out. The code identifies the stokvel, so the join URLs need no stokvel id.
public class StokvelInvite
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public string Code { get; private set; } = "";
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private StokvelInvite() { }

    public StokvelInvite(Guid stokvelId, string code, Guid createdByUserId, DateTime nowUtc, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        Code = code;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
    }
}