namespace RondiTrack.Authorization;

public static class Policies
{
    public const string StokvelMember = "StokvelMember";
    public const string StokvelTreasurer = "StokvelTreasurer";
    public const string StokvelAdmin = "StokvelAdmin";
    public const string RecordContribution = "RecordContribution";
    public const string SelfOrPlatformAdmin = "SelfOrPlatformAdmin";
}

public static class PlatformRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
}

public static class StokvelAuthFailures
{
    public const string NotMember = "stokvel-not-member";
    public const string InsufficientRole = "stokvel-insufficient-role";
}