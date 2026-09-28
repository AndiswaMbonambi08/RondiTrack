// Maps every /api/users route, annotated with OpenAPI summaries, descriptions, and
// every realistic response type so Scalar shows a complete contract.
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
        })
        .WithSummary("List all users")
        .WithDescription("Returns every user currently stored. Never fails; an empty store returns an empty array.")
        .Produces<IEnumerable<UserResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("User not found.");
            return Results.Ok(user.ToResponse());
        })
        .WithSummary("Get a user by id")
        .WithDescription("""
            Example response (200):
            { "id": "...", "fullName": "Thandiwe Nkosi", "email": "thandiwe@example.com", "isActive": true }
            """)
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (UserRequest request, IUserRepository repo) =>
        {
            var user = new User(request.FullName, request.Email);
            await repo.AddAsync(user);
            return Results.Created($"/api/users/{user.Id}", user.ToResponse());
        })
        .WithSummary("Create a user")
        .WithDescription("""
            Creates a new, active user. Does not accept an isActive field; new users always start active.
            Example request:
            { "fullName": "Zanele Khumalo", "email": "zanele@example.com" }
            Example response (201):
            { "id": "...", "fullName": "Zanele Khumalo", "email": "zanele@example.com", "isActive": true }
            """)
        .Produces<UserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<UserRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, UserRequest request, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("User not found.");

            user.UpdateDetails(request.FullName, request.Email);
            return Results.Ok(user.ToResponse());
        })
        .WithSummary("Update a user's name and email")
        .WithDescription("Does not change isActive; there is no endpoint to activate or deactivate a user yet.")
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<UserRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IUserRepository repo) =>
        {
            var user = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("User not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithSummary("Delete a user")
        .WithDescription("Does not check whether the user belongs to any stokvel; deleting a user does not remove their membership records.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}