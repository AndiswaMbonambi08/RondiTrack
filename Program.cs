using Microsoft.OpenApi;
using FluentValidation;
using RondiTrack.Authorization;
using RondiTrack.Browser;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Endpoints;
using RondiTrack.ErrorHandling;
using RondiTrack.Jobs;
using RondiTrack.Services;
using RondiTrack.Telemetry;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;
using OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Fail fast: refuse to start when a required setting is missing, and name it.
// This runs before anything else, so a misconfigured container exits at startup
// and never fails on the first request.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("RondiTrack")))
    throw new InvalidOperationException(
        "Missing required setting 'ConnectionStrings:RondiTrack' " +
        "(environment variable ConnectionStrings__RondiTrack).");

// [1] OpenTelemetry: traces, metrics and logs. The OTLP exporter is only added when
// OTEL_EXPORTER_OTLP_ENDPOINT is set, so tests and CI never need a dashboard.
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ronditrack-api")) // sets the service name the assignment required, so the dashboard shows ronditrack-api
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o =>
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
        .AddSource("Npgsql")   // gives one span per SQL command
        .AddSource(RondiTrackTelemetry.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(RondiTrackTelemetry.MeterName))
    .WithLogging();

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    otel.UseOtlpExporter(); // exports only when the variable is set. Tests and CI don't set it, so they never need a dashboard
}

builder.Services.Configure<OpenTelemetryLoggerOptions>(o =>
{
    o.IncludeScopes = true;                 // added IncludeScopes and ActivityTrackingOptions, these put the TraceId and SpanId on console log lines, so I can match a log line to the correlationId in a response
    o.IncludeFormattedMessage = true;
});

// Console lines carry the trace id too, so a log line can be matched to a response's correlationId.
builder.Logging.Configure(o =>
    o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
builder.Logging.AddSimpleConsole(o => o.IncludeScopes = true);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
JwtOptions.EnsureValid(jwtOptions);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// 6.4: job settings are validated at startup too, so a bad value fails immediately and names the setting.
var cycleCloseOptions = builder.Configuration.GetSection(CycleCloseOptions.SectionName).Get<CycleCloseOptions>() ?? new CycleCloseOptions();
CycleCloseOptions.EnsureValid(cycleCloseOptions);
builder.Services.Configure<CycleCloseOptions>(builder.Configuration.GetSection(CycleCloseOptions.SectionName));

var housekeepingOptions = builder.Configuration.GetSection(HousekeepingOptions.SectionName).Get<HousekeepingOptions>() ?? new HousekeepingOptions();
HousekeepingOptions.EnsureValid(housekeepingOptions);
builder.Services.Configure<HousekeepingOptions>(builder.Configuration.GetSection(HousekeepingOptions.SectionName));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // keep "sub" as "sub"
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            // The default is 5 minutes, which would quietly make a 15-minute token valid for 20.
            ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

// Secure by default: anything without [AllowAnonymous] or an explicit policy needs a signed-in user.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.StokvelMember, p => p.RequireAuthenticatedUser()
        .AddRequirements(new StokvelMemberRequirement(StokvelMemberRole.Member)))
    .AddPolicy(Policies.StokvelTreasurer, p => p.RequireAuthenticatedUser()
        .AddRequirements(new StokvelMemberRequirement(StokvelMemberRole.Treasurer)))
    .AddPolicy(Policies.StokvelAdmin, p => p.RequireAuthenticatedUser()
        .AddRequirements(new StokvelMemberRequirement(StokvelMemberRole.Admin)))
    .AddPolicy(Policies.RecordContribution, p => p.RequireAuthenticatedUser()
        .AddRequirements(new RecordContributionRequirement()))
    .AddPolicy(Policies.SelfOrPlatformAdmin, p => p.RequireAuthenticatedUser()
        .AddRequirements(new SelfOrPlatformAdminRequirement()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStokvelMembershipLookup, StokvelMembershipLookup>();
builder.Services.AddScoped<IAuthorizationHandler, StokvelMemberHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RecordContributionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SelfOrPlatformAdminHandler>();
builder.Services.AddScoped<StokvelAccess>();
builder.Services.AddScoped<PendingMemberRoles>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();
builder.Services.AddSingleton<ITokenService, TokenService>();


builder.Services.AddOpenApi(options =>
{
    // The API adds correlationId to every problem response. Declare it so the generated client types have it.
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonTypeInfo.Type == typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String };
        }
        return Task.CompletedTask;
    });
});

// Strict numbers: the web defaults let "100" bind to a number and make the OpenAPI document
// describe numbers as "number or string". Strict fixes both. A JSON number sent as a string
// now fails to bind and returns 400.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// Binding failures throw instead of returning a bare 400, so they reach RondiTrackExceptionHandler
// and get the problem+json body with a correlationId. Without this the default is true only in
// Development, so production would skip the error contract.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

// CORS: one named policy for the browser frontend. Origins come from the Cors:AllowedOrigins config section.
builder.Services.AddFrontendCors();

// Rate limiting: named policies, limits read from the RateLimit config section.
builder.Services.AddRateLimiting();

// [3] Every problem response carries correlationId = the request's W3C trace id.
builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx =>            // adds the correlationId to the framework's own problem responses
        ctx.ProblemDetails.Extensions["correlationId"] = CorrelationId.For(ctx.HttpContext));

builder.Services.AddExceptionHandler<RondiTrackExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// DbContext must be Scoped, never Singleton: one instance per request. A
// Singleton DbContext would be shared and mutated by every request at once,
// with no isolation between them, and EF's change tracker would grow forever
// without ever being cleared.
builder.Services.AddDbContext<RondiTrackDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("RondiTrack"),
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// [7] Probes. Live runs no checks. Ready checks PostgreSQL with a capped timeout.
// Health:ReadyTimeoutSeconds = 0 removes the cap (use it for the "before" timing).
var readyTimeoutSeconds = builder.Configuration.GetValue("Health:ReadyTimeoutSeconds", 2); // caps each check at Health:ReadyTimeoutSeconds, default 2
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RondiTrackDbContext>("postgres", HealthStatus.Unhealthy, ["ready"]); // where Health checks happened

if (readyTimeoutSeconds > 0)
{
    builder.Services.Configure<HealthCheckServiceOptions>(o =>
    {
        foreach (var registration in o.Registrations)
            registration.Timeout = TimeSpan.FromSeconds(readyTimeoutSeconds);
    });
}

// Time is a dependency: everything that needs "now" takes a TimeProvider. Tests swap in a fake.
builder.Services.AddSingleton(TimeProvider.System);

// Swapped to EF Core. Scoped because they depend on the Scoped DbContext.
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionCycleRepository, EfContributionCycleRepository>();
builder.Services.AddScoped<IPayoutRepository, EfPayoutRepository>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IStokvelMemberRepository, EfStokvelMemberRepository>();
builder.Services.AddScoped<ContributionQueryService>();
builder.Services.AddScoped<MemberQueryService>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<HomeQueryService>();

// Database-backed, so idempotency keys survive restarts and are shared by every replica.
builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

// Not swapped yet: a stated decision, not an oversight. Users are still mirrored in memory. See README.
builder.Services.AddSingleton<InMemoryUserRepository>();
builder.Services.AddScoped<IUserRepository, MirroringUserRepository>();
builder.Services.AddScoped<IStokvelService, StokvelService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserCredentialStore, EfUserCredentialStore>();
builder.Services.AddScoped<IAuthService, AuthService>();

// 6.4: background jobs. Both are safe to run on several replicas at once (see README).
builder.Services.AddHostedService<CycleCloseJob>();
builder.Services.AddHostedService<HousekeepingJob>();

var app = builder.Build();

// after app is built, before MapControllers():
app.UseExceptionHandler();
app.UseStatusCodePages();               // [3] bare 401/403 become problem details with correlationId
app.UseCors(FrontendCors.PolicyName);   // answers preflights itself, so it must run before any token check
app.UseAuthentication();
app.UseRateLimiter();                   // after authentication (so money limits can use the user id), before authorization
app.UseAuthorization();

// Runs after every request; reconciles Stokvel's in-memory member list
// against the real database table and saves everything in one go. See
// Data/SaveChangesMiddleware.cs for why this is needed instead of calling
// SaveChangesAsync directly in the repository.
// [7] Skipped for /health so a probe never touches the database through this middleware.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseMiddleware<SaveChangesMiddleware>());

// 6.4: the container runs as Production, so the docs are switched on by configuration, not only by environment.
// Set Docs__Enabled=false to hide them (see "Before production" in the README).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Docs:Enabled", false))
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// [7] Anonymous, and they only ever return Healthy or Unhealthy.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready") // only run checks tagged ready
}).AllowAnonymous();

app.MapUserEndpoints();
app.MapAuthEndpoints();
app.MapStokvelEndpoints();
app.MapContributionCycleEndpoints();
app.MapPayoutEndpoints();
app.MapPagedListEndpoints();
app.MapPayoutVersionEndpoints();
app.MapMeEndpoints();

// Migrations + demo seed, under one advisory lock so replicas queue instead of racing.
// See Data/DatabaseInitializer.cs and the README (--scale api=2).
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

app.Run();

public partial class Program { }
