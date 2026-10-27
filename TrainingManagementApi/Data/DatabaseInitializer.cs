using Microsoft.EntityFrameworkCore;

namespace TrainingManagementApi.Data;

public static class DatabaseInitializer
{
    private const int DefaultAttempts = 5;
    private const int DefaultDelaySeconds = 3;

    /// <summary>
    /// Creates/updates the database from the EF Core migrations when the API starts, so a fresh laptop only
    /// needs <c>dotnet run</c>. Settings (all optional) under <c>Database</c> in appsettings:
    /// <c>MigrateOnStartup</c> (true), <c>StartupRetries</c> (5) and <c>StartupRetryDelaySeconds</c> (3).
    ///
    /// The database server may still be starting, so a few attempts are made. If all fail
    /// the problem is logged, never thrown: the API stays up and requests that need the database return a clean
    /// 500 instead of the whole process dying. Bad setting values fall back to the defaults for the same reason.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        if (!ReadBool(app.Configuration["Database:MigrateOnStartup"], defaultValue: true))
        {
            app.Logger.LogInformation("Database:MigrateOnStartup is false - skipping automatic migrations.");
            return;
        }

        var attempts = Math.Max(1, ReadInt(app.Configuration["Database:StartupRetries"], DefaultAttempts));
        var delay = TimeSpan.FromSeconds(Math.Max(0, ReadInt(app.Configuration["Database:StartupRetryDelaySeconds"], DefaultDelaySeconds)));

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (db.Database.IsSqlite())
                {
                    // The EF migrations are written for SQL Server; for the SQLite fallback the same tables are created from the model.
                    await db.Database.EnsureCreatedAsync(app.Lifetime.ApplicationStopping);
                }
                else
                {
                    await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
                }
                app.Logger.LogInformation("Database is up to date.");
                return;
            }
            catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                return; // the application is shutting down anyway
            }
            catch (Exception ex)
            {
                if (attempt == attempts)
                {
                    app.Logger.LogCritical(ex,
                        "Could not apply database migrations after {Attempts} attempt(s). The API keeps running, but " +
                        "database calls will fail (HTTP 500) until the database is reachable and the API is restarted. " +
                        "Check ConnectionStrings:DefaultConnection. With LocalDB, 'The system cannot find the file specified' " +
                        "usually means it is not installed or the instance is missing: run 'sqllocaldb info' " +
                        "(create it with 'sqllocaldb create MSSQLLocalDB'); see README section 1.", attempts);
                    return;
                }

                app.Logger.LogWarning(
                    "Database is not ready (attempt {Attempt} of {Attempts}): {Reason} Retrying in {Delay} second(s)...",
                    attempt, attempts, ex.GetBaseException().Message, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, app.Lifetime.ApplicationStopping);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static bool ReadBool(string? value, bool defaultValue) =>
        bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static int ReadInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) ? parsed : defaultValue;
}
