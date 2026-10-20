namespace RondiTrack.Jobs;

public class HousekeepingOptions
{
    public const string SectionName = "Housekeeping";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;

    // N: refresh tokens are deleted this many days AFTER they expired (never before).
    public int RefreshTokenRetentionDays { get; set; } = 30;

    // M: idempotency records are deleted once they are older than this many hours.
    public int IdempotencyRetentionHours { get; set; } = 168;

    public static void EnsureValid(HousekeepingOptions o)
    {
        if (o.IntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Housekeeping:IntervalMinutes must be between 1 and 1440.");
        if (o.RefreshTokenRetentionDays is < 1 or > 365)
            throw new InvalidOperationException("Housekeeping:RefreshTokenRetentionDays must be between 1 and 365.");
        if (o.IdempotencyRetentionHours is < 1 or > 8760)
            throw new InvalidOperationException("Housekeeping:IdempotencyRetentionHours must be between 1 and 8760.");
    }
}