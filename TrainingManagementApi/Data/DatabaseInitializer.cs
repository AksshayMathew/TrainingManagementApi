using Microsoft.EntityFrameworkCore;

namespace TrainingManagementApi.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates/updates the database from the EF Core migrations when the API starts, so a fresh laptop only
    /// needs <c>dotnet run</c>. Controlled by <c>Database:MigrateOnStartup</c> in appsettings.
    /// A failure is logged, never thrown: the API stays up and requests that need the database
    /// return a clean 500 until the database becomes reachable.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", true))
        {
            app.Logger.LogInformation("Database:MigrateOnStartup is false - skipping automatic migrations.");
            return;
        }

        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("Database is up to date.");
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex,
                "Could not apply database migrations. The API keeps running, but database calls will fail " +
                "(HTTP 500) until the database is reachable. Check ConnectionStrings:DefaultConnection.");
        }
    }
}
