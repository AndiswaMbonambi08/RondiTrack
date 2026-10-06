using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Paging;

public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title, detail) = ex switch
        {
            InvalidPagingException e => (400, "Bad Request", e.Message),
            DbUpdateConcurrencyException
                => (412, "Precondition Failed", "The resource was modified by someone else. Re-fetch it and retry."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (409, "Conflict", "A record with the same values already exists."),
            _ => (0, "", "")
        };
        if (status == 0) return false;

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = ctx.Request.Path },
            options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}