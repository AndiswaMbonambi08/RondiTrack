// Maps every /api/stokvels route. No try/catch — validation happens via ValidationFilter,
// and every thrown exception is caught by the centralized RondiTrackExceptionHandler.
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Dtos;
using RondiTrack.Mapping;
using RondiTrack.Services;
using RondiTrack.Validation;

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
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");
            return Results.Ok(stokvel.ToResponse());
        });

        group.MapPost("/", async (StokvelRequest request, IStokvelRepository repo) =>
        {
            var stokvel = new Stokvel(request.Name, request.ContributionAmount);
            await repo.AddAsync(stokvel);
            return Results.Created($"/api/stokvels/{stokvel.Id}", stokvel.ToResponse());
        }).AddEndpointFilter<ValidationFilter<StokvelRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, StokvelRequest request, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            stokvel.UpdateDetails(request.Name, request.ContributionAmount);
            return Results.Ok(stokvel.ToResponse());
        }).AddEndpointFilter<ValidationFilter<StokvelRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/members", async (Guid id, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");
            return Results.Ok(stokvel.MemberIds);
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, IStokvelService service) =>
        {
            var response = await service.AddMemberAsync(id, request.UserId);
            return Results.Created($"/api/stokvels/{id}", response);
        }).AddEndpointFilter<ValidationFilter<AddMemberRequest>>();

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IStokvelRepository repo) =>
        {
            var stokvel = await repo.GetByIdAsync(id)
                ?? throw new NotFoundException("Stokvel not found.");

            stokvel.RemoveMember(userId);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/contributions", async (
            Guid id,
            RecordContributionRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IStokvelService service) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new RequestValidationException("An Idempotency-Key header is required to record a contribution.");

            var response = await service.RecordContributionAsync(id, request, idempotencyKey);
            return Results.Created($"/api/stokvels/{id}/contributions/{response.Id}", response);
        }).AddEndpointFilter<ValidationFilter<RecordContributionRequest>>();
    }
}