using System.Net;
using System.Net.Http.Json;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

public class TrainingAssignmentsApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public TrainingAssignmentsApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ---------- helpers ----------

    private async Task<int> CreateEmployeeAsync(string name = "Trainee")
    {
        var response = await _client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(name: name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsAsync<EmployeeDto>()).EmployeeId;
    }

    private async Task<int> CreateCourseAsync(string name = "A Course", string trainer = "A Trainer")
    {
        var response = await _client.PostAsJsonAsync("/api/courses", TestData.NewCourse(name: name, trainer: trainer));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsAsync<CourseDto>()).CourseId;
    }

    private static object NewAssignment(
        int employeeId, int courseId,
        string start = "2026-02-01T00:00:00", string end = "2026-02-05T00:00:00", string? status = null) => new
        {
            employeeId,
            courseId,
            startDate = start,
            endDate = end,
            status
        };

    private async Task<TrainingAssignmentDto> AssignAsync(int employeeId, int courseId,
        string start = "2026-02-01T00:00:00", string end = "2026-02-05T00:00:00", string? status = null)
    {
        var response = await _client.PostAsJsonAsync("/api/training-assignments",
            NewAssignment(employeeId, courseId, start, end, status));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<TrainingAssignmentDto>();
    }

    // ---------- Assign a course to an employee ----------

    [Fact]
    public async Task Assign_WithValidData_Returns201_WithLocation_AndFullDetails()
    {
        var employeeId = await CreateEmployeeAsync("Meera Nair");
        var courseId = await CreateCourseAsync("Docker Basics", "Kiran Rao");

        var response = await _client.PostAsJsonAsync("/api/training-assignments", NewAssignment(employeeId, courseId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsAsync<TrainingAssignmentDto>();
        Assert.True(created.AssignmentId > 0);
        Assert.Equal(employeeId, created.EmployeeId);
        Assert.Equal("Meera Nair", created.EmployeeName);
        Assert.Equal(courseId, created.CourseId);
        Assert.Equal("Docker Basics", created.CourseName);
        Assert.Equal("Kiran Rao", created.TrainerName);
        Assert.Equal(new DateTime(2026, 2, 1), created.StartDate);
        Assert.Equal(new DateTime(2026, 2, 5), created.EndDate);
        Assert.Equal("Assigned", created.Status);   // default when the client does not send one
        Assert.EndsWith($"/api/training-assignments/{created.AssignmentId}", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("in progress", "In Progress")]
    [InlineData("COMPLETED", "Completed")]
    [InlineData("assigned", "Assigned")]
    public async Task Assign_WithAStatus_StoresItInCanonicalForm(string sent, string expected)
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync(), status: sent);

        Assert.Equal(expected, created.Status);
    }

    [Fact]
    public async Task Assign_WhenEndDateEqualsStartDate_IsAllowed()
    {
        var response = await _client.PostAsJsonAsync("/api/training-assignments",
            NewAssignment(await CreateEmployeeAsync(), await CreateCourseAsync(),
                start: "2026-03-10T00:00:00", end: "2026-03-10T00:00:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Assign_WithUnknownEmployee_Returns404_WithMessage()
    {
        var courseId = await CreateCourseAsync();

        var response = await _client.PostAsJsonAsync("/api/training-assignments", NewAssignment(999999, courseId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("Employee with id 999999 was not found.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Assign_WithUnknownCourse_Returns404_WithMessage()
    {
        var employeeId = await CreateEmployeeAsync();

        var response = await _client.PostAsJsonAsync("/api/training-assignments", NewAssignment(employeeId, 999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("Course with id 999999 was not found.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Assign_TheSameCourseTwiceToTheSameEmployee_Returns400()
    {
        var employeeId = await CreateEmployeeAsync();
        var courseId = await CreateCourseAsync();
        await AssignAsync(employeeId, courseId);

        var response = await _client.PostAsJsonAsync("/api/training-assignments",
            NewAssignment(employeeId, courseId, start: "2026-06-01T00:00:00", end: "2026-06-02T00:00:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.Contains("already assigned", Assert.Single(errors["CourseId"]));
    }

    [Fact]
    public async Task Assign_TheSameCourseToDifferentEmployees_AndDifferentCoursesToOneEmployee_IsAllowed()
    {
        var employeeA = await CreateEmployeeAsync();
        var employeeB = await CreateEmployeeAsync();
        var course1 = await CreateCourseAsync();
        var course2 = await CreateCourseAsync();

        await AssignAsync(employeeA, course1);
        await AssignAsync(employeeB, course1);   // same course, other employee
        await AssignAsync(employeeA, course2);   // same employee, other course
    }

    [SqlServerFact]
    public async Task Assign_TheSameCourseAtTheSameTime_OnlyOneSucceeds_AndNoneFailWith500()
    {
        var employeeId = await CreateEmployeeAsync();
        var courseId = await CreateCourseAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _client.PostAsJsonAsync("/api/training-assignments", NewAssignment(employeeId, courseId))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
    }

    // ---------- Validation ----------

    public static TheoryData<string, string> InvalidAssignmentBodies => new()
    {
        { """{"courseId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00"}""", "EmployeeId" },
        { """{"employeeId":0,"courseId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00"}""", "EmployeeId" },
        { """{"employeeId":-3,"courseId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00"}""", "EmployeeId" },
        { """{"employeeId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00"}""", "CourseId" },
        { """{"employeeId":1,"courseId":0,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00"}""", "CourseId" },
        { """{"employeeId":1,"courseId":1,"endDate":"2026-02-05T00:00:00"}""", "StartDate" },
        { """{"employeeId":1,"courseId":1,"startDate":"2026-02-01T00:00:00"}""", "EndDate" },
        { """{"employeeId":1,"courseId":1,"startDate":"2026-02-10T00:00:00","endDate":"2026-02-09T00:00:00"}""", "EndDate" },
        { """{"employeeId":1,"courseId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00","status":"Finished"}""", "Status" },
        { """{"employeeId":1,"courseId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00","status":""}""", "Status" },
    };

    [Theory]
    [MemberData(nameof(InvalidAssignmentBodies))]
    public async Task Assign_WithInvalidData_Returns400_NamingTheBadField(string json, string expectedField)
    {
        var response = await _client.PostAsync("/api/training-assignments", ApiTestHelpers.JsonBody(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.True(errors.ContainsKey(expectedField), $"No error for {expectedField}. Got: {string.Join(", ", errors.Keys)}");
    }

    [Fact]
    public async Task Assign_WithEndDateBeforeStartDate_SaysSoClearly()
    {
        var response = await _client.PostAsJsonAsync("/api/training-assignments",
            NewAssignment(await CreateEmployeeAsync(), await CreateCourseAsync(),
                start: "2026-05-10T00:00:00", end: "2026-05-01T00:00:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.Equal("End date cannot be before start date.", Assert.Single(errors["EndDate"]));
    }

    [Fact]
    public async Task Assign_WithInvalidStatus_ListsTheAllowedValues()
    {
        var response = await _client.PostAsJsonAsync("/api/training-assignments",
            NewAssignment(await CreateEmployeeAsync(), await CreateCourseAsync(), status: "Finished"));

        var errors = await response.ReadErrorsAsync();
        Assert.Equal("Status must be one of: Assigned, In Progress, Completed.", Assert.Single(errors["Status"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ nope")]
    [InlineData("""{"employeeId":"one","courseId":1,"startDate":"2026-02-01T00:00:00","endDate":"2026-02-05T00:00:00"}""")]
    public async Task Assign_WithBrokenJson_Returns400_NotA500(string body)
    {
        var response = await _client.PostAsync("/api/training-assignments", ApiTestHelpers.JsonBody(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Read ----------

    [Fact]
    public async Task GetById_ReturnsTheAssignment()
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());

        var response = await _client.GetAsync($"/api/training-assignments/{created.AssignmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.AssignmentId, (await response.ReadAsAsync<TrainingAssignmentDto>()).AssignmentId);
    }

    [Fact]
    public async Task GetById_WithUnknownId_Returns404_WithMessage()
    {
        var response = await _client.GetAsync("/api/training-assignments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("Training assignment with id 999999 was not found.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetById_WithNonNumericId_Returns400()
    {
        var response = await _client.GetAsync("/api/training-assignments/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- View all trainings for an employee ----------

    [Fact]
    public async Task GetForEmployee_ReturnsOnlyThatEmployeesTrainings_OrderedByStartDate()
    {
        var employeeId = await CreateEmployeeAsync();
        var otherEmployeeId = await CreateEmployeeAsync();
        var lateCourse = await CreateCourseAsync("Late Course");
        var earlyCourse = await CreateCourseAsync("Early Course");

        var late = await AssignAsync(employeeId, lateCourse, start: "2026-09-01T00:00:00", end: "2026-09-02T00:00:00");
        var early = await AssignAsync(employeeId, earlyCourse, start: "2026-01-01T00:00:00", end: "2026-01-02T00:00:00");
        await AssignAsync(otherEmployeeId, earlyCourse);

        var response = await _client.GetAsync($"/api/employees/{employeeId}/trainings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var trainings = await response.ReadAsAsync<List<TrainingAssignmentDto>>();
        Assert.Equal(new[] { early.AssignmentId, late.AssignmentId }, trainings.Select(t => t.AssignmentId));
        Assert.All(trainings, t => Assert.Equal(employeeId, t.EmployeeId));
        Assert.Equal(new[] { "Early Course", "Late Course" }, trainings.Select(t => t.CourseName));
    }

    [Fact]
    public async Task GetForEmployee_WithNoTrainings_Returns200_AndAnEmptyList()
    {
        var employeeId = await CreateEmployeeAsync();

        var response = await _client.GetAsync($"/api/employees/{employeeId}/trainings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadAsAsync<List<TrainingAssignmentDto>>());
    }

    [Fact]
    public async Task GetForEmployee_WithUnknownEmployee_Returns404()
    {
        var response = await _client.GetAsync("/api/employees/999999/trainings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetForEmployee_WithNonNumericId_Returns400()
    {
        var response = await _client.GetAsync("/api/employees/abc/trainings");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetForEmployee_ShowsTheEmployeesCurrentName_AfterTheEmployeeIsRenamed()
    {
        var employeeId = await CreateEmployeeAsync("Old Name");
        await AssignAsync(employeeId, await CreateCourseAsync());

        var current = await (await _client.GetAsync($"/api/employees/{employeeId}")).ReadAsAsync<EmployeeDto>();
        await _client.PutAsJsonAsync($"/api/employees/{employeeId}",
            TestData.NewEmployee(current.Email, name: "New Name"));

        var trainings = await (await _client.GetAsync($"/api/employees/{employeeId}/trainings"))
            .ReadAsAsync<List<TrainingAssignmentDto>>();
        Assert.Equal("New Name", Assert.Single(trainings).EmployeeName);
    }

    // ---------- Update status ----------

    [Fact]
    public async Task UpdateStatus_ChangesTheStatus_AndReturns200()
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());

        var response = await _client.PutAsJsonAsync($"/api/training-assignments/{created.AssignmentId}/status",
            new { status = "In Progress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("In Progress", (await response.ReadAsAsync<TrainingAssignmentDto>()).Status);

        var reloaded = await (await _client.GetAsync($"/api/training-assignments/{created.AssignmentId}"))
            .ReadAsAsync<TrainingAssignmentDto>();
        Assert.Equal("In Progress", reloaded.Status);
    }

    [Fact]
    public async Task UpdateStatus_CanMoveThroughTheWholeLifecycle()
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());
        var url = $"/api/training-assignments/{created.AssignmentId}/status";

        foreach (var status in new[] { "In Progress", "Completed" })
        {
            var response = await _client.PutAsJsonAsync(url, new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(status, (await response.ReadAsAsync<TrainingAssignmentDto>()).Status);
        }
    }

    [Theory]
    [InlineData("completed", "Completed")]
    [InlineData("IN-PROGRESS", "In Progress")]
    public async Task UpdateStatus_AcceptsDifferentCasing_AndStoresTheCanonicalValue(string sent, string expected)
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());

        var response = await _client.PutAsJsonAsync($"/api/training-assignments/{created.AssignmentId}/status",
            new { status = sent });

        Assert.Equal(expected, (await response.ReadAsAsync<TrainingAssignmentDto>()).Status);
    }

    [Theory]
    [InlineData("""{"status":"Finished"}""")]
    [InlineData("""{"status":""}""")]
    [InlineData("""{"status":null}""")]
    [InlineData("""{}""")]
    public async Task UpdateStatus_WithInvalidStatus_Returns400_AndLeavesItUnchanged(string body)
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());

        var response = await _client.PutAsync($"/api/training-assignments/{created.AssignmentId}/status",
            ApiTestHelpers.JsonBody(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Status", (await response.ReadErrorsAsync()).Keys);
        var reloaded = await (await _client.GetAsync($"/api/training-assignments/{created.AssignmentId}"))
            .ReadAsAsync<TrainingAssignmentDto>();
        Assert.Equal("Assigned", reloaded.Status);
    }

    [Fact]
    public async Task UpdateStatus_WithUnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/training-assignments/999999/status", new { status = "Completed" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithEmptyBody_Returns400()
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());

        var response = await _client.PutAsync($"/api/training-assignments/{created.AssignmentId}/status",
            ApiTestHelpers.JsonBody(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheAssignment()
    {
        var created = await AssignAsync(await CreateEmployeeAsync(), await CreateCourseAsync());

        var response = await _client.DeleteAsync($"/api/training-assignments/{created.AssignmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(created.AssignmentId.ToString(), (await response.ReadAsAsync<MessageDto>()).Message);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/training-assignments/{created.AssignmentId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_ThenAssignTheSameCourseAgain_Works()
    {
        var employeeId = await CreateEmployeeAsync();
        var courseId = await CreateCourseAsync();
        var created = await AssignAsync(employeeId, courseId);
        await _client.DeleteAsync($"/api/training-assignments/{created.AssignmentId}");

        var response = await _client.PostAsJsonAsync("/api/training-assignments", NewAssignment(employeeId, courseId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnknownId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/training-assignments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Protection of training history ----------

    [Fact]
    public async Task DeleteEmployee_WhoHasTrainings_Returns400_UntilTheTrainingsAreDeleted()
    {
        var employeeId = await CreateEmployeeAsync();
        var assignment = await AssignAsync(employeeId, await CreateCourseAsync());

        var blocked = await _client.DeleteAsync($"/api/employees/{employeeId}");

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        var errors = await blocked.ReadErrorsAsync();
        Assert.Contains("still has training assignments", Assert.Single(errors["EmployeeId"]));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/employees/{employeeId}")).StatusCode);

        await _client.DeleteAsync($"/api/training-assignments/{assignment.AssignmentId}");
        var allowed = await _client.DeleteAsync($"/api/employees/{employeeId}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task DeleteCourse_ThatIsAssigned_Returns400_UntilTheTrainingsAreDeleted()
    {
        var courseId = await CreateCourseAsync();
        var assignment = await AssignAsync(await CreateEmployeeAsync(), courseId);

        var blocked = await _client.DeleteAsync($"/api/courses/{courseId}");

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        var errors = await blocked.ReadErrorsAsync();
        Assert.Contains("still assigned", Assert.Single(errors["CourseId"]));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/courses/{courseId}")).StatusCode);

        await _client.DeleteAsync($"/api/training-assignments/{assignment.AssignmentId}");
        var allowed = await _client.DeleteAsync($"/api/courses/{courseId}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
