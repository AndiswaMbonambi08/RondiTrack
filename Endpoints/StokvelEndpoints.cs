//Maps /api/stokvels CRUD plus the nested /api/stokvels/{id}/members routes. 
//Translates domain exceptions into status codes here.
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Endpoints;

public record CreateStokvelRequest(string Name, decimal ContributionAmount);
public record UpdateStokvelRequest(string Name, decimal ContributionAmount);
public record AddMemberRequest(Guid UserId);

public static class StokvelEndpoints
{
    public static void MapStokvelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stokvels").WithTags("Stokvels");

        group.MapGet("/", async (IStokvelRepository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            return stokvel is null ? Results.NotFound() : Results.Ok(stokvel);
        });

        group.MapPost("/", async (CreateStokvelRequest request, IStokvelRepository repo) =>
        {
            try
            {
                var stokvel = new Stokvel(request.Name, request.ContributionAmount);
                await repo.AddAsync(stokvel);
                return Results.Created($"/api/stokvels/{stokvel.Id}", stokvel);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateStokvelRequest request, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            if (stokvel is null) return Results.NotFound();

            try
            {
                stokvel.UpdateDetails(request.Name, request.ContributionAmount);
                return Results.Ok(stokvel);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            if (stokvel is null) return Results.NotFound();

            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/members", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            return stokvel is null ? Results.NotFound() : Results.Ok(stokvel.MemberIds);
        });

        group.MapPost("/{id:guid}/members", async (
            Guid id, AddMemberRequest request, IStokvelRepository stokvelRepo, IUserRepository userRepo) =>
        {
            var stokvel = await stokvelRepo.GetByIdAsync(id);
            if (stokvel is null) return Results.NotFound(new { error = "Stokvel not found." });

            var user = await userRepo.GetByIdAsync(request.UserId);
            if (user is null) return Results.NotFound(new { error = "User not found." });

            try
            {
                stokvel.AddMember(user);
                return Results.Created($"/api/stokvels/{id}/members/{user.Id}", stokvel.MemberIds);
            }
            catch (DuplicateMemberException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InactiveUserException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            if (stokvel is null) return Results.NotFound();

            try
            {
                stokvel.RemoveMember(userId);
                return Results.NoContent();
            }
            catch (MemberNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });
    }
}