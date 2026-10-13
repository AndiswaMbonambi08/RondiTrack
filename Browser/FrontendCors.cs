using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace RondiTrack.Browser;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";

    // Empty by default (appsettings.json). Development adds http://localhost:3000.
    public string[] AllowedOrigins { get; set; } = [];
}

public static class FrontendCors
{
    // The only place the policy is named. Endpoints never mention CORS.
    public const string PolicyName = "Frontend";

    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddOptions<FrontendCorsOptions>()
            .BindConfiguration(FrontendCorsOptions.SectionName)
            .Validate(
                o => o.AllowedOrigins.All(IsExactOrigin),
                "Cors:AllowedOrigins must hold exact origins such as https://app.example.com: no '*', no path, no trailing slash.")
            .ValidateOnStart();

        services.AddCors();

        // Built lazily from the bound options, so test hosts that use UseSetting see their own origins.
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<FrontendCorsOptions>>((cors, frontend) =>
            {
                cors.AddPolicy(PolicyName, policy => policy
                    .WithOrigins(frontend.Value.AllowedOrigins)          // exact list, never "*", never the request's Origin
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", "If-Match")
                    .WithExposedHeaders("Retry-After", "ETag", "Location", "Idempotency-Replayed")
                    .AllowCredentials()                                   // the refresh cookie needs it
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
            });

        return services;
    }

    public static bool IsExactOrigin(string origin) =>
        origin != "*"
        && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

    // The exception handler clears the response before it writes, which drops any CORS headers the
    // middleware already added. A browser would then hide a 404 or 409 body from the frontend, so the
    // handler calls this to put the same policy's headers back.
    public static async Task ReapplyAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.Origin))
            return;

        var provider = context.RequestServices.GetRequiredService<ICorsPolicyProvider>();
        var service = context.RequestServices.GetRequiredService<ICorsService>();

        var policy = await provider.GetPolicyAsync(context, PolicyName);
        if (policy is null) return;

        service.ApplyResult(service.EvaluatePolicy(context, policy), context.Response);
    }
}