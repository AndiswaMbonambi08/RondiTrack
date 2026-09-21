//Maps the /api/users HTTP routes (CRUD) to the domain and repository. 
//Also holds the small request records for POST/PUT bodies.
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Endpoints;

public record CreateUserRequest(string FullName, string Email);
public record UpdateUserRequest(string FullName, string Email);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/", async (IUserRepository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPost("/", async (CreateUserRequest request, IUserRepository repo) =>
        {
            try
            {
                var user = new User(request.FullName, request.Email);
                await repo.AddAsync(user);
                return Results.Created($"/api/users/{user.Id}", user);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id);
            if (user is null) return Results.NotFound();

            try
            {
                user.UpdateDetails(request.FullName, request.Email);
                return Results.Ok(user);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id);
            if (user is null) return Results.NotFound();

            await repo.DeleteAsync(id);
            return Results.NoContent();
        });
    }
}