// The single place every unhandled exception in RondiTrack passes through. Maps known
// RondiTrackException types to their status code, falls back to 400 for entity-level
// ArgumentException, and 500 for anything truly unexpected. Every response and its
// matching log line carry the same correlation ID (the request's TraceIdentifier).
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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

        var (statusCode, title) = exception switch
        {
            RondiTrackException rte => (rte.StatusCode, rte.Title),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var detail = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : exception.Message;

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