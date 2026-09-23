using FluentValidation;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Endpoints;
using RondiTrack.ErrorHandling;
using RondiTrack.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RondiTrackExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IStokvelRepository, InMemoryStokvelRepository>();
builder.Services.AddSingleton<IContributionCycleRepository, InMemoryContributionCycleRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<IStokvelService, StokvelService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapUserEndpoints();
app.MapStokvelEndpoints();
app.MapContributionCycleEndpoints();

using (var scope = app.Services.CreateScope())
{
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var stokvelRepo = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();

    var users = await userRepo.GetAllAsync();
    var activeUsers = users.Where(u => u.IsActive).ToList();

    var stokvel = new Stokvel("Ubuntu Savings Circle", 500m);
    stokvel.AddMember(activeUsers[0]);
    stokvel.AddMember(activeUsers[1]);
    await stokvelRepo.AddAsync(stokvel);
}

app.Run();

public partial class Program { }
