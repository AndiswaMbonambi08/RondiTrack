// Maps every /api/users route. No try/catch here anymore — validation happens via
// ValidationFilter before this code runs, and any exception thrown here is caught by
// the centralized RondiTrackExceptionHandler, not locally.
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Validation;

namespace RondiTrack.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/", async (IUserRepository repo) =>
        {
            var users = await repo.GetAllAsync();
            return Results.Ok(users.Select(u => u.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("User not found.");
            return Results.Ok(user.ToResponse());
        });

        group.MapPost("/", async (UserRequest request, IUserRepository repo) =>
        {
            var user = new User(request.FullName, request.Email);
            await repo.AddAsync(user);
            return Results.Created($"/api/users/{user.Id}", user.ToResponse());
        }).AddEndpointFilter<ValidationFilter<UserRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, UserRequest request, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("User not found.");

            user.UpdateDetails(request.FullName, request.Email);
            return Results.Ok(user.ToResponse());
        }).AddEndpointFilter<ValidationFilter<UserRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("User not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        });
    }
}