using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrainingManagementApi.Data;

namespace TrainingManagementApi.Tests.Infrastructure;

/// <summary>
/// Starts the real API in memory for integration tests, with its own empty database per test class.
///
/// Default: an in-memory SQLite database (fast, needs nothing installed).
/// If the environment variable <c>TMS_TEST_SQLSERVER</c> holds a SQL Server connection string (without a
/// database name) the same tests run against a throw-away SQL Server database built from the real EF Core
/// migrations - this is how the SQL Server specific parts (migrations, unique index, FK rules) are verified.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SqlServerEnvironmentVariable = "TMS_TEST_SQLSERVER";

    public static string? SqlServerBaseConnection => Environment.GetEnvironmentVariable(SqlServerEnvironmentVariable);

    public static bool UsingSqlServer => !string.IsNullOrWhiteSpace(SqlServerBaseConnection);

    private readonly SqliteConnection? _sqliteConnection;
    private readonly string? _sqlServerConnectionString;

    public ApiFactory()
    {
        if (UsingSqlServer)
        {
            var builder = new SqlConnectionStringBuilder(SqlServerBaseConnection!)
            {
                InitialCatalog = $"TrainingMgmt_Test_{Guid.NewGuid():N}"
            };
            _sqlServerConnectionString = builder.ConnectionString;
        }
        else
        {
            // An in-memory SQLite database lives only while its connection stays open, so we hold one open.
            _sqliteConnection = new SqliteConnection("DataSource=:memory:");
            _sqliteConnection.Open();

            // SQLite ignores foreign keys unless asked; SQL Server always enforces them.
            using var pragma = _sqliteConnection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // The test creates the schema itself (EnsureCreated / Migrate below).
                ["Database:MigrateOnStartup"] = "false"
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                if (_sqlServerConnectionString is not null)
                {
                    options.UseSqlServer(_sqlServerConnectionString);
                }
                else
                {
                    options.UseSqlite(_sqliteConnection!);
                }
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (UsingSqlServer)
        {
            await db.Database.MigrateAsync();     // exercises the real migrations
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
    }

    // Explicit: WebApplicationFactory already has a ValueTask DisposeAsync(), xUnit wants a Task one.
    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_sqlServerConnectionString is not null)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _sqliteConnection?.Dispose();
        }
    }
}
