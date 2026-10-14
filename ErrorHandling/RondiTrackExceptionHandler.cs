using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Browser;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Telemetry;

namespace RondiTrack.ErrorHandling;

public partial class RondiTrackExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RondiTrackExceptionHandler> _logger;

    public RondiTrackExceptionHandler(ILogger<RondiTrackExceptionHandler> logger)
    {
        _logger = logger;
    }

    // ArgumentException.Message ends with "(Parameter 'x')", which leaks code names. Strip it.
    [GeneratedRegex(@"\s*\(Parameter '[^']*'\)\s*$")]
    private static partial Regex ParameterSuffix();

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId.For(httpContext);

        var (statusCode, title) = exception switch
        {
            RondiTrackException rte => (rte.StatusCode, rte.Title),
            // Malformed JSON, wrong types, strict-number violations. Never expose the framework type.
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad Request"),
            DbUpdateConcurrencyException => (StatusCodes.Status412PreconditionFailed, "Precondition Failed"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var detail = exception switch
        {
            BadHttpRequestException
                => "The request body is missing, malformed or has a value of the wrong type.",
            DbUpdateConcurrencyException
                => "The resource was changed by someone else since you read it. Fetch it again and retry.",
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => "A record with the same values already exists.",
            ArgumentException ae => ParameterSuffix().Replace(ae.Message, ""),
            _ when statusCode == StatusCodes.Status500InternalServerError
                => "An unexpected error occurred.",
            _ => exception.Message
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Request failed. CorrelationId: {CorrelationId}, Method: {Method}, Path: {Path}, StatusCode: {StatusCode}",
                correlationId, httpContext.Request.Method, httpContext.Request.Path, statusCode);
        }
        else
        {
            _logger.LogInformation(
                "Request rejected. CorrelationId: {CorrelationId}, Method: {Method}, Path: {Path}, StatusCode: {StatusCode}, ExceptionType: {ExceptionType}",
                correlationId, httpContext.Request.Method, httpContext.Request.Path, statusCode, exception.GetType().Name);
        }

        var problemDetails = new ProblemDetails { Status = statusCode, Title = title, Detail = detail };
        problemDetails.Extensions["correlationId"] = correlationId;

        if (exception is RequestValidationException { Errors.Count: > 0 } validation)
            problemDetails.Extensions["errors"] = validation.Errors;

        httpContext.Response.StatusCode = statusCode;

        await FrontendCors.ReapplyAsync(httpContext);

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }
}