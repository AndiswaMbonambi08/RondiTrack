using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Services;

namespace RondiTrack.Endpoints;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Me).WithTags("Me");

        group.MapGet("", async (HttpContext http, IUserCredentialStore credentials) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null)
                return AuthProblems.Unauthorized(http);

            var user = await credentials.FindByIdAsync(userId.Value);
            if (user is null || !user.IsActive)
                return AuthProblems.Unauthorized(http);

            return Results.Ok(user.ToResponse());
        })
        .WithSummary("Who the signed-in member is")
        .WithDescription("Returns the caller's own profile and never a password hash. Their role in each stokvel comes from /me/home.")
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
        group.MapGet("/home", async (HttpContext http, HomeQueryService home, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            if (userId is null)
                return AuthProblems.Unauthorized(http);

            return Results.Ok(await home.GetAsync(userId.Value, ct));
        })
        .WithSummary("The signed-in user's home screen")
        .WithDescription("""
            One entry per stokvel the caller belongs to: name, the caller's role name, the current cycle
            (newest cycle that is not yet paid out), what the caller paid and still owes this cycle, and the
            next payout date in UTC. Money is integer cents. A user in no stokvels gets 200 with an empty list.
            Only the caller's own amounts are returned.
            """)
        .Produces<HomeResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}