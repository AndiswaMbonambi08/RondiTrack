using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Endpoints;
using RondiTrack.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IStokvelRepository, InMemoryStokvelRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<IStokvelService, StokvelService>();

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
    stokvel.AddMember(users[0]);
    stokvel.AddMember(users[1]);
    await stokvelRepo.AddAsync(stokvel);
}

app.Run();
/*so what we were basically doing for this is assignment is tis....the changes i made in each file and while i doid it the way i did it, why i chose the classes and functions i chose...fuction oof all of them...must exaplain in human terms for me to actually understand what is it we were doing what was the point*/