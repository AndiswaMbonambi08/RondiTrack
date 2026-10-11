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
    }
}