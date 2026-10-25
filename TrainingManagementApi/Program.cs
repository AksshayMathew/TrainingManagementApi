using Microsoft.EntityFrameworkCore;
using TrainingManagementApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Connection string comes from appsettings.json (LocalDB by default). To use another SQL Server on your
// own machine without touching tracked files, set it with `dotnet user-secrets` or an environment variable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(connectionString))
{
    app.Logger.LogError("ConnectionStrings:DefaultConnection is not configured. Database calls will fail.");
}

await app.ApplyMigrationsAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

// Makes the entry point visible to the integration-test project (WebApplicationFactory<Program>).
public partial class Program { }
