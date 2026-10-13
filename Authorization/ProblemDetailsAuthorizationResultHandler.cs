using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using RondiTrack.Telemetry;

namespace RondiTrack.Authorization;

public static class AuthProblems //static class , the pipeline handler and the endpoints that do their own checks (StokvelAccess.cs) return exactly the same shape
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

        // Never log the stokvel name, the email, or what they sent in the request. Those could identify someone or reveal money details.
        // A refusal is normal. For example, someone clicks an old link, or a member tries something only a treasurer can do. These aren’t emergencies
        // If refusals suddenly increase, the refusals metric will show it, so we don’t need loud Warning logs
    
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
            result = AuthProblems.Unauthorized(context); //401 response is empty
        }
        else if (AuthProblems.IsNotMember(authorizeResult.AuthorizationFailure))
        {
            result = AuthProblems.NotFound(context); //404 response is empty
        }
        else
        {
            result = AuthProblems.Forbidden(context); //403 response is empty
        }

        await result.ExecuteAsync(context); //the default handler would just set the status code, no body
    }
}