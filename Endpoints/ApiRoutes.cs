namespace RondiTrack.Endpoints;

public static class ApiRoutes
{
    public const string Version = "v1";
    public const string Base = "/api/" + Version;

    public const string Auth = Base + "/auth";
    public const string Users = Base + "/users";
    public const string Stokvels = Base + "/stokvels";
    public const string ContributionCycles = Base + "/contribution-cycles";
    public const string Payouts = Base + "/payouts";
    public const string Me = Base + "/me";
}