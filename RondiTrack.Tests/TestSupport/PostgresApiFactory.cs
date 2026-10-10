using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace RondiTrack.Tests.TestSupport;

// Starts a throwaway PostgreSQL container, builds its schema from the real
// migrations, then points the API at it. The dev database is never touched.
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("ronditrack_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // The container is empty, so create the schema before the app starts.
        var options = new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var db = new RondiTrackDbContext(options);
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, NOT ConfigureAppConfiguration: Program.cs reads the
        // connection string at startup, before the latter would apply.
        builder.UseSetting("ConnectionStrings:RondiTrack", _container.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Postgres collection")]
public class PostgresCollection : ICollectionFixture<PostgresApiFactory> { }