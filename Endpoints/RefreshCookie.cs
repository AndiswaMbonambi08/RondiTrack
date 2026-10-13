namespace RondiTrack.Endpoints;

public static class RefreshCookie
{
    public const string Name = "refresh_token";

    public static void Append(HttpResponse response, string token, DateTime expiresUtc)
    {
        var options = Base();
        options.Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero);
        response.Cookies.Append(Name, token, options);
    }

    // The browser only removes a cookie when the name and Path match the original.
    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Base());

    private static CookieOptions Base() => new()
    {
        HttpOnly = true,                      // JavaScript can't read it, so an XSS bug can't steal it
        Secure = true,                        // only ever sent over HTTPS
        SameSite = SameSiteMode.Strict,       // never sent on cross-site requests
        Path = ApiRoutes.Auth                 // /api/v1/auth: only sent to the auth endpoints                   // only sent to the auth endpoints, not on every API call
    };
}