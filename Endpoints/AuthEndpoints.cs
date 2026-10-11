using RondiTrack.Dtos;
using RondiTrack.Services;
using RondiTrack.Validation;

namespace RondiTrack.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth) =>
        {
            var user = await auth.RegisterAsync(request);
            return Results.Created($"/api/users/{user.Id}", user);
        })
        .AllowAnonymous()
        .WithSummary("Register a new account")
        .WithDescription("Password must be at least 12 characters. A duplicate email returns 409.")
        .Produces<UserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost("/login", async (LoginRequest request, HttpContext http, IAuthService auth) =>
        {
            var result = await auth.LoginAsync(request);
            RefreshCookie.Append(http.Response, result.RefreshToken, result.RefreshExpiresAtUtc);
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result.Response);   // the body never contains the refresh token
        })
        .AllowAnonymous()
        .WithSummary("Sign in")
        .WithDescription("Returns a short-lived access token and sets the refresh token as an HttpOnly cookie. An unknown email and a wrong password return the same 401.")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        group.MapPost("/refresh", async (HttpContext http, IAuthService auth) =>
        {
            var result = await auth.RefreshAsync(http.Request.Cookies[RefreshCookie.Name]);
            RefreshCookie.Append(http.Response, result.RefreshToken, result.RefreshExpiresAtUtc);
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result.Response);
        })
        .AllowAnonymous()
        .WithSummary("Exchange the refresh cookie for a new access token")
        .WithDescription("The refresh token works exactly once; a new one is set in the cookie. Any failure returns the same 401.")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}