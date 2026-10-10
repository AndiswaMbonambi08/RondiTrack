using RondiTrack.Tests.TestSupport;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RondiTrack.Data;
using RondiTrack.Dtos;
using Xunit;

namespace RondiTrack.Tests.Integration;

[Collection("Postgres collection")]
public class UniqueConstraintTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public UniqueConstraintTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DuplicateContribution_IsRejectedByTheDatabase_EvenWithoutTheServiceCheck()
    {
        var user = (await (await _client.PostAsJsonAsync("/api/users", new { fullName = "U", email = $"{Guid.NewGuid()}@example.com" }))
            .Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;
        var stokvel = (await (await _client.PostAsJsonAsync("/api/stokvels", new { name = "Constraint Stokvel", contributionAmount = 100m }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;
        (await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id })).EnsureSuccessStatusCode();
        var cycle = (await (await _client.PostAsJsonAsync("/api/contribution-cycles", new { stokvelId = stokvel.Id, label = "C1", targetAmount = 100m }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        // Inserts straight into the table, skipping the service-layer check.
        // ADAPT: add any other NOT NULL columns your Contributions table has.
        Task Insert() => db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO ""Contributions"" (""Id"", ""StokvelId"", ""UserId"", ""ContributionCycleId"", ""Amount"", ""RecordedAt"")
               VALUES ({Guid.NewGuid()}, {stokvel.Id}, {user.Id}, {cycle.Id}, {100m}, {DateTime.UtcNow})");

        await Insert();
        var ex = await Record.ExceptionAsync(Insert);

        var pg = ex as PostgresException ?? ex?.InnerException as PostgresException;
        Assert.NotNull(pg);
        Assert.Equal("23505", pg!.SqlState);
    }
}
