using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace RondiTrack.Tests.TestSupport;

// Its own container with the demo seed off, so job tests get exact counts
// and cannot disturb the other tests.
public class JobsApiFactory : PostgresApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Seed:Enabled", "false");
    }
}

[CollectionDefinition("Jobs collection")]
public class JobsCollection : ICollectionFixture<JobsApiFactory> { }