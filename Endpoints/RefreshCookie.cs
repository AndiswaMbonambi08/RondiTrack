namespace RondiTrack.Endpoints;

public static class RefreshCookie
{
    public const string Name = "refresh_token";

    public static void Append(HttpResponse response, string token, DateTime expiresUtc) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,                      // JavaScript can't read it, so an XSS bug can't steal it
            Secure = true,                        // only ever sent over HTTPS
            SameSite = SameSiteMode.Strict,       // never sent on cross-site requests
            Path = "/api/auth",                   // only sent to the auth endpoints, not on every API call
            Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero)
        });
}