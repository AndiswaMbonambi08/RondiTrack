using FluentValidation;
using RondiTrack.Authorization;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Endpoints;
using RondiTrack.ErrorHandling;
using RondiTrack.Services;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;
using StokvelMemberRole = RondiTrack.Persistence.Entities.StokvelMemberRole;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
JwtOptions.EnsureValid(jwtOptions);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

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


builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RondiTrackExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// DbContext must be Scoped, never Singleton — one instance per request. A
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

// Swapped to EF Core. Scoped because they depend on the Scoped DbContext.
builder.Services.AddScoped<IStokvelRepository, EfStokvelRepository>();
builder.Services.AddScoped<IContributionCycleRepository, EfContributionCycleRepository>();
builder.Services.AddScoped<IPayoutRepository, EfPayoutRepository>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IStokvelMemberRepository, EfStokvelMemberRepository>();
builder.Services.AddScoped<ContributionQueryService>();
builder.Services.AddScoped<MemberQueryService>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();

// Not swapped yet — a stated decision, not an oversight. See README.
builder.Services.AddSingleton<InMemoryUserRepository>();
builder.Services.AddScoped<IUserRepository, MirroringUserRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddScoped<IStokvelService, StokvelService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserCredentialStore, EfUserCredentialStore>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// after app is built, before MapControllers():
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// Runs after every request; reconciles Stokvel's in-memory member list
// against the real database table and saves everything in one go. See
// Data/SaveChangesMiddleware.cs for why this is needed instead of calling
// SaveChangesAsync directly in the repository.
app.UseMiddleware<SaveChangesMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapUserEndpoints();
app.MapAuthEndpoints();
app.MapStokvelEndpoints();
app.MapContributionCycleEndpoints();
app.MapPayoutEndpoints();
app.MapPagedListEndpoints();
app.MapPayoutVersionEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var inMemoryUsers = scope.ServiceProvider.GetRequiredService<InMemoryUserRepository>();
    var stokvelRepo = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();

    var usersInDb = await db.Users.AsNoTracking().ToListAsync();

    if (usersInDb.Count > 0)
    {
        // Not the first run: the database is the source of truth. The in-memory seed users get new
        // Ids on every start, so swap them for the real rows. Ids then match the database, and users
        // who registered earlier are known to the in-memory store again.
        foreach (var seeded in (await inMemoryUsers.GetAllAsync()).ToList())
            await inMemoryUsers.DeleteAsync(seeded.Id);
        foreach (var existing in usersInDb)
            await inMemoryUsers.AddAsync(existing);
    }
    else
    {
        var users = await userRepo.GetAllAsync();

        // StokvelMembers has a foreign key to the Users table, so the users must exist there first.
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        var activeUsers = users.Where(u => u.IsActive).ToList();

        var stokvel = new Stokvel("Ubuntu Savings Circle", 500m);
        stokvel.AddMember(activeUsers[0]);
        stokvel.AddMember(activeUsers[1]);
        await stokvelRepo.AddAsync(stokvel);

        // The seed runs outside any HTTP request, so the SaveChangesMiddleware
        // never fires for it, so save the membership rows directly here instead.
        foreach (var memberId in stokvel.MemberIds)
        {
            db.StokvelMembers.Add(new RondiTrack.Persistence.Entities.StokvelMember(
                stokvel.Id, memberId, RondiTrack.Persistence.Entities.StokvelMemberRole.Member, DateTime.UtcNow));
        }
        await db.SaveChangesAsync();
    }
}

app.Run();

public partial class Program { }
