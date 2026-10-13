using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RondiTrack.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace RondiTrack.Tests.TestSupport;

// Starts a throwaway PostgreSQL container, builds its schema from the real
// migrations, then points the API at it. The dev database is never touched.
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // A throwaway key per test run: nothing secret is committed and tests don't depend on anyone's user-secrets.
    private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    // The user every default client acts as. A platform admin, so existing tests keep passing
    // with unchanged assertions: they create stokvels as a caller who is not a member.
    public static readonly Guid TestUserId = Guid.NewGuid();

    // Every log entry the app writes during the run, for the log-leak test.
    public CapturingLoggerProvider Logs { get; } = new();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
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
        builder.UseSetting("Jwt:SigningKey", _signingKey);

        // The TestServer gives every request a null RemoteIpAddress, so tests share rate-limit partitions.
        // Very high defaults keep existing tests passing with unchanged assertions. Rate-limit tests
        // override these with WithWebHostBuilder, which also gives them a fresh limiter.
        builder.UseSetting("RateLimit:Login:PermitLimit", "100000");
        builder.UseSetting("RateLimit:RecordContribution:TokenLimit", "100000");
        builder.UseSetting("RateLimit:RecordContribution:TokensPerPeriod", "100000");
        builder.UseSetting("RateLimit:ProcessPayout:TokenLimit", "100000");
        builder.UseSetting("RateLimit:ProcessPayout:TokensPerPeriod", "100000");

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }

    // Mints a valid access token directly, signed with this run's key. Faster than logging in,
    // and a bug in the login code cannot fail unrelated tests.
    public string MintToken(Guid userId, bool platformAdmin = false)
    {
        var config = Services.GetRequiredService<IConfiguration>();
        var claims = new Dictionary<string, object> { ["sub"] = userId.ToString() };
        if (platformAdmin) claims["role"] = "PlatformAdmin";

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
                SecurityAlgorithms.HmacSha256)
        });
    }

    // Every client from CreateClient() is the platform admin test user by default.
    protected override void ConfigureClient(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(TestUserId, platformAdmin: true));

    public HttpClient CreateClientFor(Guid userId, bool platformAdmin = false)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(userId, platformAdmin));
        return client;
    }

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    async Task IAsyncLifetime.DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Postgres collection")]
public class PostgresCollection : ICollectionFixture<PostgresApiFactory> { }