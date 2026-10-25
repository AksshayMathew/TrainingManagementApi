using System.Net;
using System.Net.Http.Json;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

public class CoursesApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public CoursesApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<CourseDto> CreateCourseAsync(object? payload = null)
    {
        var response = await _client.PostAsJsonAsync("/api/courses", payload ?? TestData.NewCourse());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<CourseDto>();
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_WithValidData_Returns201_WithLocationHeader_AndSavedCourse()
    {
        var response = await _client.PostAsJsonAsync("/api/courses",
            TestData.NewCourse(name: "C# Basics", duration: 24, trainer: "Rahul Verma"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsAsync<CourseDto>();
        Assert.True(created.CourseId > 0);
        Assert.Equal("C# Basics", created.CourseName);
        Assert.Equal(24, created.Duration);
        Assert.Equal("Rahul Verma", created.TrainerName);
        Assert.EndsWith($"/api/courses/{created.CourseId}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_TrimsText()
    {
        var created = await CreateCourseAsync(TestData.NewCourse(name: "  Git Basics  ", trainer: " Sam "));

        Assert.Equal("Git Basics", created.CourseName);
        Assert.Equal("Sam", created.TrainerName);
    }

    [Fact]
    public async Task Create_WithTheSameNameTwice_IsAllowed()
    {
        // The assignment does not ask for unique course names (a course can run with different trainers).
        await CreateCourseAsync(TestData.NewCourse(name: "Repeated Course"));
        var second = await _client.PostAsJsonAsync("/api/courses", TestData.NewCourse(name: "Repeated Course"));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    // ---------- Validation ----------

    public static TheoryData<string, string> InvalidCourseBodies => new()
    {
        // name
        { """{"courseName":"","duration":8,"trainerName":"Anita"}""", "CourseName" },
        { """{"courseName":"   ","duration":8,"trainerName":"Anita"}""", "CourseName" },
        { """{"duration":8,"trainerName":"Anita"}""", "CourseName" },
        // duration must be greater than zero
        { """{"courseName":"X","duration":0,"trainerName":"Anita"}""", "Duration" },
        { """{"courseName":"X","duration":-5,"trainerName":"Anita"}""", "Duration" },
        { """{"courseName":"X","trainerName":"Anita"}""", "Duration" },
        { """{"courseName":"X","duration":null,"trainerName":"Anita"}""", "Duration" },
        { """{"courseName":"X","duration":10001,"trainerName":"Anita"}""", "Duration" },
        // trainer
        { """{"courseName":"X","duration":8,"trainerName":""}""", "TrainerName" },
        { """{"courseName":"X","duration":8}""", "TrainerName" },
    };

    [Theory]
    [MemberData(nameof(InvalidCourseBodies))]
    public async Task Create_WithInvalidData_Returns400_NamingTheBadField(string json, string expectedField)
    {
        var response = await _client.PostAsync("/api/courses", ApiTestHelpers.JsonBody(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.True(errors.ContainsKey(expectedField), $"No error for {expectedField}. Got: {string.Join(", ", errors.Keys)}");
    }

    [Fact]
    public async Task Create_WithDurationZero_SaysItMustBeGreaterThanZero()
    {
        var response = await _client.PostAsJsonAsync("/api/courses", TestData.NewCourse(duration: 0));

        var errors = await response.ReadErrorsAsync();
        Assert.Contains("greater than 0", Assert.Single(errors["Duration"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ nope")]
    [InlineData("""{"courseName":"X","duration":"eight","trainerName":"Anita"}""")]
    public async Task Create_WithBrokenJson_Returns400_NotA500(string body)
    {
        var response = await _client.PostAsync("/api/courses", ApiTestHelpers.JsonBody(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Read ----------

    [Fact]
    public async Task GetById_ReturnsTheCourse()
    {
        var created = await CreateCourseAsync();

        var response = await _client.GetAsync($"/api/courses/{created.CourseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.CourseName, (await response.ReadAsAsync<CourseDto>()).CourseName);
    }

    [Fact]
    public async Task GetById_WithUnknownId_Returns404_WithMessage()
    {
        var response = await _client.GetAsync("/api/courses/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("Course with id 999999 was not found.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetById_WithNonNumericId_Returns400()
    {
        var response = await _client.GetAsync("/api/courses/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedCourses_OrderedById()
    {
        var first = await CreateCourseAsync();
        var second = await CreateCourseAsync();

        var response = await _client.GetAsync("/api/courses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var all = await response.ReadAsAsync<List<CourseDto>>();
        Assert.Contains(all, c => c.CourseId == first.CourseId);
        Assert.Contains(all, c => c.CourseId == second.CourseId);
        Assert.Equal(all.Select(c => c.CourseId).OrderBy(id => id), all.Select(c => c.CourseId));
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_ChangesTheCourse_AndReturns200()
    {
        var created = await CreateCourseAsync();

        var response = await _client.PutAsJsonAsync($"/api/courses/{created.CourseId}",
            TestData.NewCourse(name: "Renamed", duration: 40, trainer: "New Trainer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await (await _client.GetAsync($"/api/courses/{created.CourseId}")).ReadAsAsync<CourseDto>();
        Assert.Equal("Renamed", reloaded.CourseName);
        Assert.Equal(40, reloaded.Duration);
        Assert.Equal("New Trainer", reloaded.TrainerName);
    }

    [Fact]
    public async Task Update_WithInvalidData_Returns400_AndLeavesTheCourseUnchanged()
    {
        var created = await CreateCourseAsync();

        var response = await _client.PutAsJsonAsync($"/api/courses/{created.CourseId}", TestData.NewCourse(duration: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reloaded = await (await _client.GetAsync($"/api/courses/{created.CourseId}")).ReadAsAsync<CourseDto>();
        Assert.Equal(created.Duration, reloaded.Duration);
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/courses/999999", TestData.NewCourse());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheCourse()
    {
        var created = await CreateCourseAsync();

        var deleteResponse = await _client.DeleteAsync($"/api/courses/{created.CourseId}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains(created.CourseId.ToString(), (await deleteResponse.ReadAsAsync<MessageDto>()).Message);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/courses/{created.CourseId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnknownId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/courses/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
