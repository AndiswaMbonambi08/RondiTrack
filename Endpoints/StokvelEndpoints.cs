// Maps every /api/stokvels route, including nested members and contributions routes.
// Membership and contribution decisions are delegated to IStokvelService; this file only
// translates HTTP <-> DTOs and turns exceptions into Problem Details responses.
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Services;

namespace RondiTrack.Endpoints;

public static class StokvelEndpoints
{
    public static void MapStokvelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stokvels").WithTags("Stokvels");

        group.MapGet("/", async (IStokvelRepository repo) =>
        {
            var stokvels = await repo.GetAllAsync();
            return Results.Ok(stokvels.Select(s => s.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            return stokvel is null
                ? ProblemResponses.NotFound("Stokvel not found.")
                : Results.Ok(stokvel.ToResponse());
        });

        group.MapPost("/", async (StokvelRequest request, IStokvelRepository repo) =>
        {
            try
            {
                var stokvel = new Stokvel(request.Name, request.ContributionAmount);
                await repo.AddAsync(stokvel);
                return Results.Created($"/api/stokvels/{stokvel.Id}", stokvel.ToResponse());
            }
            catch (ArgumentException ex)
            {
                return ProblemResponses.BadRequest(ex.Message);
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, StokvelRequest request, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            if (stokvel is null) return ProblemResponses.NotFound("Stokvel not found.");

            try
            {
                stokvel.UpdateDetails(request.Name, request.ContributionAmount);
                return Results.Ok(stokvel.ToResponse());
            }
            catch (ArgumentException ex)
            {
                return ProblemResponses.BadRequest(ex.Message);
            }
        });

        group.MapDelete("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            if (stokvel is null) return ProblemResponses.NotFound("Stokvel not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/members", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            return stokvel is null
                ? ProblemResponses.NotFound("Stokvel not found.")
                : Results.Ok(stokvel.MemberIds);
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, IStokvelService service) =>
        {
            try
            {
                var response = await service.AddMemberAsync(id, request.UserId);
                return Results.Created($"/api/stokvels/{id}", response);
            }
            catch (ResourceNotFoundException ex)
            {
                return ProblemResponses.NotFound(ex.Message);
            }
            catch (DuplicateMemberException ex)
            {
                return ProblemResponses.Conflict(ex.Message);
            }
            catch (InactiveUserException ex)
            {
                return ProblemResponses.Conflict(ex.Message);
            }
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id);
            if (stokvel is null) return ProblemResponses.NotFound("Stokvel not found.");

            try
            {
                stokvel.RemoveMember(userId);
                return Results.NoContent();
            }
            catch (MemberNotFoundException ex)
            {
                return ProblemResponses.NotFound(ex.Message);
            }
        });

        group.MapPost("/{id:guid}/contributions", async (
            Guid id,
            RecordContributionRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IStokvelService service) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return ProblemResponses.BadRequest("An Idempotency-Key header is required to record a contribution.");

            try
            {
                var response = await service.RecordContributionAsync(id, request, idempotencyKey);
                return Results.Created($"/api/stokvels/{id}/contributions/{response.Id}", response);
            }
            catch (ResourceNotFoundException ex)
            {
                return ProblemResponses.NotFound(ex.Message);
            }
            catch (MemberNotFoundException ex)
            {
                return ProblemResponses.Conflict(ex.Message);
            }
            catch (DuplicateContributionException ex)
            {
                return ProblemResponses.Conflict(ex.Message);
            }
            catch (IdempotencyMismatchException ex)
            {
                return ProblemResponses.Unprocessable(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ProblemResponses.BadRequest(ex.Message);
            }
        });
    }
}