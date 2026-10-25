using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrainingManagementApi.Data;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

/// <summary>The API must start - and stay up - even when the database is missing or the settings are wrong.</summary>
public class StartupResilienceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StartupResilienceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>A client for an API whose database cannot be opened and which tries to migrate while starting.</summary>
    private HttpClient CreateClientWithUnreachableDatabase(Dictionary<string, string?> settings) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(ApiTestHelpers.UnopenableSqliteConnectionString()));
            });
        }).CreateClient();

    [Fact]
    public async Task UnreachableDatabaseAtStartup_DoesNotStopTheApi()
    {
        using var client = CreateClientWithUnreachableDatabase(new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "true",
            ["Database:StartupRetries"] = "2",
            ["Database:StartupRetryDelaySeconds"] = "0"
        });

        // The host started (otherwise CreateClient/GetAsync would have thrown) and answers requests.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Fact]
    public async Task NonsenseSettingValues_FallBackToDefaults_InsteadOfCrashing()
    {
        using var client = CreateClientWithUnreachableDatabase(new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "maybe",            // not a bool   -> default (true)
            ["Database:StartupRetries"] = "1",                  // keep the test fast
            ["Database:StartupRetryDelaySeconds"] = "not-a-number"   // not an int  -> default (only used between retries)
        });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task MissingConnectionString_DoesNotStopTheApi()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "",
                    ["Database:MigrateOnStartup"] = "true",
                    ["Database:StartupRetries"] = "1"
                }));
            builder.ConfigureTestServices(services =>
            {
                // Use the real SQL Server registration from Program.cs with an empty connection string.
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlServer(string.Empty));
            });
        }).CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/api/employees")).StatusCode);
    }
}
