using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

/// <summary>The console shows ONE clear line per API call - including WHY a call was rejected.</summary>
public class RequestLoggingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RequestLoggingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private (HttpClient Client, ConcurrentQueue<(string Category, LogLevel Level, string Message)> Log) CreateClientWithLog()
    {
        var log = new ConcurrentQueue<(string, LogLevel, string)>();
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(new ListLoggerProvider(log)))).CreateClient();
        return (client, log);
    }

    private static string[] RequestLines(ConcurrentQueue<(string Category, LogLevel Level, string Message)> log) =>
        log.Where(entry => entry.Category == "Request").Select(entry => $"{entry.Level}|{entry.Message}").ToArray();

    [Fact]
    public async Task SuccessfulCall_IsOneInformationLine_WithStatusAndTime()
    {
        var (client, log) = CreateClientWithLog();

        var response = await client.PostAsJsonAsync("/api/employees", TestData.NewEmployee());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var line = Assert.Single(RequestLines(log));
        Assert.StartsWith("Information|POST /api/employees -> 201 (", line);
        Assert.EndsWith(" ms)", line);
    }

    [Fact]
    public async Task DuplicateEmail_IsOneWarningLine_ThatSaysWhy()
    {
        var (client, log) = CreateClientWithLog();
        var email = TestData.UniqueEmail();
        await client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(email));

        var response = await client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var line = RequestLines(log).Last();
        Assert.StartsWith("Warning|POST /api/employees -> 400 An employee with this email already exists.", line);
        Assert.Contains("Use a different e-mail address.", (await response.ReadErrorsAsync())["Email"].Single());
    }

    [Fact]
    public async Task InvalidInput_LogLineNamesTheWrongFields()
    {
        var (client, log) = CreateClientWithLog();

        var response = await client.PostAsync("/api/employees", ApiTestHelpers.JsonBody(
            """{"employeeName":"","email":"not-an-email","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var line = RequestLines(log).Single();
        Assert.Contains("-> 400", line);
        Assert.Contains("EmployeeName: Employee name is required.", line);
        Assert.Contains("Email: Email must be a valid e-mail address.", line);
        // the answer itself is unchanged: standard validation JSON
        Assert.Contains("EmployeeName", (await response.ReadErrorsAsync()).Keys);
    }

    [Fact]
    public async Task NotFound_IsOneWarningLine_ThatSaysWhat()
    {
        var (client, log) = CreateClientWithLog();

        await client.GetAsync("/api/courses/987654");

        Assert.Equal("Warning|GET /api/courses/987654 -> 404 Course with id 987654 was not found. (" +
                     RequestLines(log).Single().Split('(').Last(), RequestLines(log).Single());
    }

    [Fact]
    public async Task SwaggerPageViews_AreNotLoggedAsApiCalls()
    {
        var (client, log) = CreateClientWithLog();

        await client.GetAsync("/swagger/index.html");
        await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Empty(RequestLines(log));
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string, LogLevel, string)> _entries;

        public ListLoggerProvider(ConcurrentQueue<(string, LogLevel, string)> entries) => _entries = entries;

        public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class ListLogger : ILogger
        {
            private readonly string _category;
            private readonly ConcurrentQueue<(string, LogLevel, string)> _entries;

            public ListLogger(string category, ConcurrentQueue<(string, LogLevel, string)> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                _entries.Enqueue((_category, logLevel, formatter(state, exception)));
        }
    }
}
