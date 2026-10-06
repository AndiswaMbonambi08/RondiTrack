// The single place every unhandled exception in RondiTrack passes through. Maps known
// RondiTrackException types to their status code, maps database errors to 409 (unique
// violation) and 412 (stale concurrency token), falls back to 400 for entity-level
// ArgumentException, and 500 for anything truly unexpected. Every response and its
// matching log line carry the same correlation ID (the request's TraceIdentifier).
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.ErrorHandling;

public class RondiTrackExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RondiTrackExceptionHandler> _logger;

    public RondiTrackExceptionHandler(ILogger<RondiTrackExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        // DbUpdateConcurrencyException derives from DbUpdateException, so it must be matched first.
        var (statusCode, title) = exception switch
        {
            RondiTrackException rte => (rte.StatusCode, rte.Title),
            DbUpdateConcurrencyException => (StatusCodes.Status412PreconditionFailed, "Precondition Failed"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        // Database errors get a fixed message, never the raw exception text (it can name tables and columns).
        var detail = exception switch
        {
            DbUpdateConcurrencyException
                => "The resource was changed by someone else since you read it. Fetch it again and retry.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => "A record with the same values already exists.",
            _ when statusCode == StatusCodes.Status500InternalServerError
                => "An unexpected error occurred.",
            _ => exception.Message
        };

        _logger.LogError(
            exception,
            "Request failed. CorrelationId: {CorrelationId}, Method: {Method}, Path: {Path}, StatusCode: {StatusCode}",
            correlationId, httpContext.Request.Method, httpContext.Request.Path, statusCode);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };
        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }
}