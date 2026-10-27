using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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

// SQL Server is the default. If no SQL Server is available, set Database:Provider to "Sqlite" (see the README and the
// "http-sqlite" launch profile): same API, same tables, stored in a single file.
var useSqlite = string.Equals(builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Services (business rules). Scoped = one instance per HTTP request, matching the DbContext lifetime.
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ITrainingAssignmentService, TrainingAssignmentService>();

// One line per log entry with a short time stamp (the default prints two lines per entry).
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Same 400 answer as the built-in one, plus a short reason for the one-line request log
    // (for example "Email: Email must be a valid e-mail address.").
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemFactory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = problemFactory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);

        context.HttpContext.Items[RequestLoggingMiddleware.ReasonKey] =
            string.Join(" ", details.Errors.Take(3).Select(error => $"{error.Key}: {error.Value.FirstOrDefault()}"));

        return new BadRequestObjectResult(details)
        {
            ContentTypes = { "application/problem+json", "application/problem+xml" }
        };
    };
});
builder.Services.AddApiDocumentation();   // Swagger - see Swagger/SwaggerConfiguration.cs

var app = builder.Build();

if (string.IsNullOrWhiteSpace(connectionString))
{
    app.Logger.LogError("ConnectionStrings:DefaultConnection is not configured. Database calls will fail.");
}

await app.ApplyMigrationsAsync();

// Order matters: the request log is outermost (it sees the final answer), then the error handler, which must be able
// to catch exceptions from everything below it.
app.UseMiddleware<RequestLoggingMiddleware>();
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
