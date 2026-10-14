using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RondiTrack.Authorization;
using RondiTrack.Telemetry;

namespace RondiTrack.Browser;

public sealed class LoginRateLimitOptions
{
    public const string SectionName = "RateLimit:Login";

    public int PermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
}

// Token bucket settings for the money endpoints. One named instance per policy.
public sealed class MoneyRateLimitOptions
{
    public int TokenLimit { get; set; } = 30;          // bucket size = the largest burst allowed
    public int TokensPerPeriod { get; set; } = 1;      // tokens added each period
    public int ReplenishSeconds { get; set; } = 10;    // length of one period
}

public static class RateLimiting
{
    // The only place policy names are defined. Endpoints refer to these constants.
    public const string LoginPolicy = "login";
    public const string RecordContributionPolicy = "record-contribution";
    public const string ProcessPayoutPolicy = "process-payout";

    private const int FallbackRetryAfterSeconds = 60;

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<LoginRateLimitOptions>()
            .BindConfiguration(LoginRateLimitOptions.SectionName)
            .Validate(o => o.PermitLimit > 0 && o.WindowSeconds > 0,
                "RateLimit:Login:PermitLimit and WindowSeconds must both be greater than zero.")
            .ValidateOnStart();

        AddMoneyOptions(services, RecordContributionPolicy, "RateLimit:RecordContribution");
        AddMoneyOptions(services, ProcessPayoutPolicy, "RateLimit:ProcessPayout");

        services.AddRateLimiter();

        // Built lazily from the bound options, so test hosts that use UseSetting get their own limits and a fresh limiter.
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<LoginRateLimitOptions>, IOptionsMonitor<MoneyRateLimitOptions>>((limiter, login, money) =>
            {
                var loginSettings = login.Value;

                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Login: fixed window per IP. Nobody is signed in yet, so the IP is all there is.
                limiter.AddPolicy(LoginPolicy, http =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        IpKey(http),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = loginSettings.PermitLimit,
                            Window = TimeSpan.FromSeconds(loginSettings.WindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

                // Money endpoints: token bucket per signed-in user (the sub claim).
                AddMoneyPolicy(limiter, money.Get(RecordContributionPolicy), RecordContributionPolicy);
                AddMoneyPolicy(limiter, money.Get(ProcessPayoutPolicy), ProcessPayoutPolicy);

                limiter.OnRejected = async (context, cancellationToken) =>
                {
                    var http = context.HttpContext;

                    var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        : FallbackRetryAfterSeconds;

                    http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    http.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Too many requests. Wait before trying again."
                    };
                    problem.Extensions["correlationId"] = CorrelationId.For(http);

                    await http.Response.WriteAsJsonAsync(
                        problem,
                        options: (JsonSerializerOptions?)null,
                        contentType: "application/problem+json",
                        cancellationToken);
                };
            });

        return services;
    }

    private static void AddMoneyOptions(IServiceCollection services, string name, string section) =>
        services.AddOptions<MoneyRateLimitOptions>(name)
            .BindConfiguration(section)
            .Validate(o => o.TokenLimit > 0 && o.TokensPerPeriod > 0 && o.ReplenishSeconds > 0,
                $"{section}: TokenLimit, TokensPerPeriod and ReplenishSeconds must all be greater than zero.")
            .ValidateOnStart();

    private static void AddMoneyPolicy(RateLimiterOptions limiter, MoneyRateLimitOptions settings, string name) =>
        limiter.AddPolicy(name, http =>
            RateLimitPartition.GetTokenBucketLimiter(
                UserOrIpKey(http),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = settings.TokenLimit,
                    TokensPerPeriod = settings.TokensPerPeriod,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(settings.ReplenishSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    private static string IpKey(HttpContext http) =>
        $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    // The limiter runs after UseAuthentication, so User is already populated here.
    // Unauthenticated requests fall back to the IP partition.
    private static string UserOrIpKey(HttpContext http) =>
        http.User.GetUserId() is { } id ? $"user:{id}" : IpKey(http);
}