using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrainingManagementApi.Data;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Interfaces;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

/// <summary>Proves the "API never crashes" requirement: whatever goes wrong, the client gets clean JSON.</summary>
public class ErrorHandlingTests : IClassFixture<ApiFactory>
{
    private const string SecretInternalDetail = "secret-internal-detail-do-not-leak";

    private readonly ApiFactory _factory;

    public ErrorHandlingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithBrokenEmployeeService(string environment) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureTestServices(services =>
                services.AddScoped<IEmployeeService, ThrowingEmployeeService>());
        }).CreateClient();

    [Fact]
    public async Task UnhandledException_Returns500_AsProblemJson_WithTraceId()
    {
        using var client = CreateClientWithBrokenEmployeeService("Production");

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task UnhandledException_InProduction_DoesNotLeakInternalDetails()
    {
        using var client = CreateClientWithBrokenEmployeeService("Production");

        var response = await client.GetAsync("/api/employees");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(SecretInternalDetail, body);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_ShowsTheReasonToHelpDebugging()
    {
        using var client = CreateClientWithBrokenEmployeeService("Development");

        var response = await client.GetAsync("/api/employees");
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(SecretInternalDetail, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task DatabaseUnavailable_Returns500_AndTheApiKeepsServingOtherRequests()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                // A database file in a folder that does not exist: opening it fails, like an unreachable server would.
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(ApiTestHelpers.UnopenableSqliteConnectionString()));
            })).CreateClient();

        var failing = await client.GetAsync("/api/employees");
        Assert.Equal(HttpStatusCode.InternalServerError, failing.StatusCode);
        Assert.Equal("application/problem+json", failing.Content.Headers.ContentType?.MediaType);

        // Still alive: a request that does not need the database works, and the failing one can be repeated.
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var again = await client.GetAsync("/api/employees");
        Assert.Equal(HttpStatusCode.InternalServerError, again.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class ThrowingEmployeeService : IEmployeeService
    {
        private static InvalidOperationException Boom() => new(SecretInternalDetail);

        public Task<PagedResult<EmployeeDto>> GetAllAsync(EmployeeQuery query, CancellationToken cancellationToken = default) => throw Boom();
        public Task<EmployeeDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw Boom();
        public Task<EmployeeDto> CreateAsync(EmployeeRequestDto request, CancellationToken cancellationToken = default) => throw Boom();
        public Task<EmployeeDto> UpdateAsync(int id, EmployeeRequestDto request, CancellationToken cancellationToken = default) => throw Boom();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw Boom();
    }
}
