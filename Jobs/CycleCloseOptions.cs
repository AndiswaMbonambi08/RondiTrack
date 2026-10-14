namespace RondiTrack.Jobs;

public class CycleCloseOptions
{
    public const string SectionName = "CycleClose";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 15;
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    public int CycleEndDay { get; set; } = 25;

    public TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"CycleClose:TimeZoneId '{TimeZoneId}' was not found on this machine " +
                "(environment variable CycleClose__TimeZoneId). In a container the image needs the tzdata package.", ex);
        }
    }

    // Called at startup so a bad value fails immediately and names the setting.
    public static void EnsureValid(CycleCloseOptions o)
    {
        if (o.IntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException("CycleClose:IntervalMinutes must be between 1 and 1440.");
        if (o.CycleEndDay is < 1 or > 28)
            throw new InvalidOperationException("CycleClose:CycleEndDay must be between 1 and 28.");
        o.ResolveTimeZone();
    }
}