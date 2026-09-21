//Wires up DI, OpenAPI/Scalar, maps the endpoint groups, seeds one stokvel with members on startup.
using Microsoft.AspNetCore.OpenApi;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IStokvelRepository, InMemoryStokvelRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapUserEndpoints();
app.MapStokvelEndpoints();

using (var scope = app.Services.CreateScope())
{
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var stokvelRepo = scope.ServiceProvider.GetRequiredService<IStokvelRepository>();

    var users = await userRepo.GetAllAsync();
    var stokvel = new Stokvel("Ubuntu Savings Circle", 500m);
        
    foreach (var user in users.Where(u => u.IsActive))
    {
        stokvel.AddMember(user);
    }
    await stokvelRepo.AddAsync(stokvel);
}
app.Run();
