using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

/// <summary>The "no SQL Server available" fallback: Database:Provider = Sqlite creates and uses a single database file.</summary>
public class SqliteFallbackTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tms-sqlite-" + Guid.NewGuid().ToString("N"));

    public SqliteFallbackTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public async Task WithProviderSqlite_TheApiCreatesItsOwnDatabaseFile_AndWorksEndToEnd()
    {
        var dbFile = Path.Combine(_folder, "TrainingManagement.db");

        // A plain factory (not ApiFactory): the application's own start-up code chooses SQLite and creates the file.
        await using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbFile}");
            builder.UseSetting("Database:MigrateOnStartup", "true");
        }))
        {
            using var client = factory.CreateClient();

            var created = await client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(name: "Sqlite Person"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var employee = await created.ReadAsAsync<EmployeeDto>();

            var course = await (await client.PostAsJsonAsync("/api/courses", TestData.NewCourse())).ReadAsAsync<CourseDto>();
            var assigned = await client.PostAsJsonAsync("/api/training-assignments", new
            {
                employeeId = employee.EmployeeId,
                courseId = course.CourseId,
                startDate = "2026-02-01T00:00:00",
                endDate = "2026-02-05T00:00:00"
            });
            Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);

            // the database rules still hold: same course twice -> 400; employee with trainings cannot be deleted
            var again = await client.PostAsJsonAsync("/api/training-assignments", new
            {
                employeeId = employee.EmployeeId,
                courseId = course.CourseId,
                startDate = "2026-03-01T00:00:00",
                endDate = "2026-03-02T00:00:00"
            });
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/employees/{employee.EmployeeId}")).StatusCode);

            var trainings = await (await client.GetAsync($"/api/employees/{employee.EmployeeId}/trainings"))
                .ReadAsAsync<List<TrainingAssignmentDto>>();
            Assert.Single(trainings);
        }

        SqliteConnection.ClearAllPools();
        Assert.True(File.Exists(dbFile), "the SQLite database file should have been created by the application");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // a file still in use by the OS is not worth failing a test for
        }
    }
}
