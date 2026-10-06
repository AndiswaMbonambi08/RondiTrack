using FluentValidation;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Endpoints;
using RondiTrack.ErrorHandling;
using RondiTrack.Services;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

// Not swapped yet — a stated decision, not an oversight. See README.
builder.Services.AddSingleton<InMemoryUserRepository>();
builder.Services.AddScoped<IUserRepository, MirroringUserRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddScoped<IStokvelService, StokvelService>();

var app = builder.Build();

// after app is built, before MapControllers():
app.UseExceptionHandler();

// Runs after every request; reconciles Stokvel's in-memory member list
// against the real database table and saves everything in one go. See
// Data/SaveChangesMiddleware.cs for why this is needed instead of calling
// SaveChangesAsync directly in the repository.
app.UseMiddleware<SaveChangesMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapUserEndpoints();
app.MapStokvelEndpoints();
app.MapContributionCycleEndpoints();
app.MapPayoutEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var stokvelRepo = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();

    var users = await userRepo.GetAllAsync();

    // StokvelMembers has a foreign key to the Users table, so the users must exist there first.
    var existingIds = await db.Users.Select(u => u.Id).ToListAsync();
    db.Users.AddRange(users.Where(u => !existingIds.Contains(u.Id)));
    await db.SaveChangesAsync();

    var activeUsers = users.Where(u => u.IsActive).ToList();

    var stokvel = new Stokvel("Ubuntu Savings Circle", 500m);
    stokvel.AddMember(activeUsers[0]);
    stokvel.AddMember(activeUsers[1]);
    await stokvelRepo.AddAsync(stokvel);

    // The seed runs outside any HTTP request, so the SaveChangesMiddleware
    // never fires for it — save the membership rows directly here instead.
    foreach (var memberId in stokvel.MemberIds)
    {
       db.StokvelMembers.Add(new RondiTrack.Persistence.Entities.StokvelMember(
       stokvel.Id, memberId, RondiTrack.Persistence.Entities.StokvelMemberRole.Member, DateTime.UtcNow));
        
    }
    await db.SaveChangesAsync();
}

app.Run();

public partial class Program { }
