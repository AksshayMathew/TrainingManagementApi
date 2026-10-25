using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TrainingManagementApi.Data;
using TrainingManagementApi.Interfaces;
using TrainingManagementApi.Middleware;
using TrainingManagementApi.Services;

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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Employee Training Management API",
        Version = "v1",
        Description = "ABC Technologies - manage employees, training courses and the training assigned to each employee."
    });

    // Show the /// comments from the code as descriptions in Swagger. Guarded so a missing file can never stop startup.
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

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

// Swagger stays on in every environment because this is a training project. For production, disable it or protect it.
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Training Management API v1"));

app.MapControllers();

app.Run();

// Makes the entry point visible to the integration-test project (WebApplicationFactory<Program>).
public partial class Program { }
