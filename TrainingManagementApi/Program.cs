using Microsoft.EntityFrameworkCore;
using TrainingManagementApi.Data;
using TrainingManagementApi.Interfaces;
using TrainingManagementApi.Middleware;
using TrainingManagementApi.Services;
using TrainingManagementApi.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Connection string comes from appsettings.json (LocalDB by default). To use another SQL Server on your
// own machine without touching tracked files, set it with `dotnet user-secrets` or an environment variable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// Services (business rules). Scoped = one instance per HTTP request, matching the DbContext lifetime.
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ITrainingAssignmentService, TrainingAssignmentService>();

builder.Services.AddControllers();
builder.Services.AddApiDocumentation();   // Swagger - see Swagger/SwaggerConfiguration.cs

var app = builder.Build();

if (string.IsNullOrWhiteSpace(connectionString))
{
    app.Logger.LogError("ConnectionStrings:DefaultConnection is not configured. Database calls will fail.");
}

await app.ApplyMigrationsAsync();

// Must be first so it can catch exceptions from everything below it.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseApiDocumentation();

app.MapControllers();

app.Run();

// Makes the entry point visible to the integration-test project (WebApplicationFactory<Program>).
public partial class Program { }
