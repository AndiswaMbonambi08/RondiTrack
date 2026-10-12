using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using RondiTrack.Telemetry;

namespace RondiTrack.Authorization;

// One place that builds the 401, 403 and 404 problem responses, so the pipeline and the endpoints
// that run their own checks return exactly the same shape.
public static class AuthProblems
{
    public static IResult Unauthorized(HttpContext ctx) =>
        Create(ctx, 401, "Unauthorized", "Authentication is required.");

    public static IResult NotFound(HttpContext ctx, string? detail = null)
    {
        Refused(ctx, RefusalResult.NotFound);
        return Create(ctx, 404, "Not Found", detail ?? "The requested stokvel was not found.");
    }

    public static IResult Forbidden(HttpContext ctx)
    {
        Refused(ctx, RefusalResult.Forbidden);
        return Create(ctx, 403, "Forbidden", "You do not have permission to perform this action.");
    }

    private static void Refused(HttpContext ctx, RefusalResult result)
    {
        RondiTrackTelemetry.AuthorizationRefused(result);

        // Own user id only (the "sub" claim). Never the stokvel name, email or request body.
        // Information, not Warning: a stale link or a member trying a higher-role action is routine,
        // and a spike shows up in the refusals metric. Warning stays reserved for token reuse.
        ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RondiTrack.Authorization")
            .LogInformation(
                "Authorization refused. Result: {Result}, UserId: {UserId}",
                result == RefusalResult.NotFound ? "not_found" : "forbidden",
                ctx.User.FindFirst("sub")?.Value);
    }

    public static bool IsNotMember(AuthorizationFailure? failure) =>
        failure?.FailureReasons.Any(x => x.Message == StokvelAuthFailures.NotMember) == true;

    private static IResult Create(HttpContext ctx, int status, string title, string detail) =>
        Results.Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] = CorrelationId.For(ctx)
            });
}

public sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        IResult result;
        if (authorizeResult.Challenged)
        {
            context.Response.Headers.Append("WWW-Authenticate", "Bearer");
            result = AuthProblems.Unauthorized(context);
        }
        else if (AuthProblems.IsNotMember(authorizeResult.AuthorizationFailure))
        {
            result = AuthProblems.NotFound(context);
        }
        else
        {
            result = AuthProblems.Forbidden(context);
        }

        await result.ExecuteAsync(context);
    }
}