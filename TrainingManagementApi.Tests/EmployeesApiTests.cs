using System.Net;
using System.Net.Http.Json;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

public class EmployeesApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public EmployeesApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<EmployeeDto> CreateEmployeeAsync(object? payload = null)
    {
        var response = await _client.PostAsJsonAsync("/api/employees", payload ?? TestData.NewEmployee());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<EmployeeDto>();
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_WithValidData_Returns201_WithLocationHeader_AndSavedEmployee()
    {
        var email = TestData.UniqueEmail();

        var response = await _client.PostAsJsonAsync("/api/employees",
            TestData.NewEmployee(email, name: "Priya Sharma", department: "HR", experience: 5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsAsync<EmployeeDto>();
        Assert.True(created.EmployeeId > 0);
        Assert.Equal("Priya Sharma", created.EmployeeName);
        Assert.Equal(email, created.Email);
        Assert.Equal("HR", created.Department);
        Assert.Equal(new DateTime(2026, 1, 15), created.JoiningDate);
        Assert.Equal(5, created.Experience);

        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/employees/{created.EmployeeId}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_WithZeroExperience_IsAllowed()
    {
        var created = await CreateEmployeeAsync(TestData.NewEmployee(experience: 0));
        Assert.Equal(0, created.Experience);
    }

    [Fact]
    public async Task Create_TrimsTextAndStoresEmailInLowerCase()
    {
        var unique = Guid.NewGuid().ToString("N");

        var created = await CreateEmployeeAsync(new
        {
            employeeName = "  Ravi Kumar  ",
            email = $"  Ravi.{unique}@Example.COM ",
            department = "  Finance ",
            joiningDate = "2026-03-01T00:00:00",
            experience = 2
        });

        Assert.Equal("Ravi Kumar", created.EmployeeName);
        Assert.Equal($"ravi.{unique}@example.com", created.Email);
        Assert.Equal("Finance", created.Department);
    }

    // ---------- Validation (HTTP 400 with a clear message per field) ----------

    public static TheoryData<string, string> InvalidEmployeeBodies => new()
    {
        // name
        { """{"employeeName":"","email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "EmployeeName" },
        { """{"employeeName":"   ","email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "EmployeeName" },
        { """{"email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "EmployeeName" },
        { """{"employeeName":null,"email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "EmployeeName" },
        // email
        { """{"employeeName":"A","email":"","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "Email" },
        { """{"employeeName":"A","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "Email" },
        { """{"employeeName":"A","email":"not-an-email","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":1}""", "Email" },
        // department
        { """{"employeeName":"A","email":"a@example.com","department":"","joiningDate":"2026-01-15T00:00:00","experience":1}""", "Department" },
        { """{"employeeName":"A","email":"a@example.com","joiningDate":"2026-01-15T00:00:00","experience":1}""", "Department" },
        // joining date
        { """{"employeeName":"A","email":"a@example.com","department":"IT","experience":1}""", "JoiningDate" },
        { """{"employeeName":"A","email":"a@example.com","department":"IT","joiningDate":null,"experience":1}""", "JoiningDate" },
        // experience
        { """{"employeeName":"A","email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":-1}""", "Experience" },
        { """{"employeeName":"A","email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":61}""", "Experience" },
        { """{"employeeName":"A","email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00"}""", "Experience" },
    };

    [Theory]
    [MemberData(nameof(InvalidEmployeeBodies))]
    public async Task Create_WithInvalidData_Returns400_NamingTheBadField(string json, string expectedField)
    {
        var response = await _client.PostAsync("/api/employees", ApiTestHelpers.JsonBody(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.True(errors.TryGetValue(expectedField, out var messages), $"No error for {expectedField}. Got: {string.Join(", ", errors.Keys)}");
        Assert.All(messages!, message => Assert.False(string.IsNullOrWhiteSpace(message)));
    }

    [Fact]
    public async Task Create_WithTooLongName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/employees",
            TestData.NewEmployee(name: new string('x', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("EmployeeName", (await response.ReadErrorsAsync()).Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ this is not json")]
    [InlineData("""{"employeeName":"A","email":"a@example.com","department":"IT","joiningDate":"not-a-date","experience":1}""")]
    [InlineData("""{"employeeName":"A","email":"a@example.com","department":"IT","joiningDate":"2026-01-15T00:00:00","experience":"many"}""")]
    public async Task Create_WithBrokenJson_Returns400_NotA500(string body)
    {
        var response = await _client.PostAsync("/api/employees", ApiTestHelpers.JsonBody(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Unique e-mail ----------

    [Fact]
    public async Task Create_WithDuplicateEmail_Returns400_WithClearMessage()
    {
        var email = TestData.UniqueEmail();
        await CreateEmployeeAsync(TestData.NewEmployee(email));

        var response = await _client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.Contains("already exists", Assert.Single(errors["Email"]));
    }

    [Fact]
    public async Task Create_WithSameEmailInDifferentCase_IsTreatedAsDuplicate()
    {
        var email = TestData.UniqueEmail();
        await CreateEmployeeAsync(TestData.NewEmployee(email));

        var response = await _client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", (await response.ReadErrorsAsync()).Keys);
    }

    [SqlServerFact]
    public async Task Create_WithSameEmailAtTheSameTime_OnlyOneSucceeds_AndNoneFailWith500()
    {
        var email = TestData.UniqueEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(email))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
    }

    // ---------- Read ----------

    [Fact]
    public async Task GetById_ReturnsTheEmployee()
    {
        var created = await CreateEmployeeAsync();

        var response = await _client.GetAsync($"/api/employees/{created.EmployeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.ReadAsAsync<EmployeeDto>();
        Assert.Equal(created.EmployeeId, fetched.EmployeeId);
        Assert.Equal(created.Email, fetched.Email);
    }

    [Fact]
    public async Task GetById_WithUnknownId_Returns404_WithMessage()
    {
        var response = await _client.GetAsync("/api/employees/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("Employee with id 999999 was not found.", problem.GetProperty("detail").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999999")]
    public async Task GetById_WithNonNumericId_Returns400(string id)
    {
        var response = await _client.GetAsync($"/api/employees/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task GetById_WithNonPositiveId_Returns404(string id)
    {
        var response = await _client.GetAsync($"/api/employees/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedEmployees_OrderedById()
    {
        var first = await CreateEmployeeAsync();
        var second = await CreateEmployeeAsync();

        var response = await _client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var all = await response.ReadAsAsync<List<EmployeeDto>>();
        Assert.Contains(all, e => e.EmployeeId == first.EmployeeId);
        Assert.Contains(all, e => e.EmployeeId == second.EmployeeId);
        Assert.Equal(all.Select(e => e.EmployeeId).OrderBy(id => id), all.Select(e => e.EmployeeId));
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_ChangesTheEmployee_AndReturns200()
    {
        var created = await CreateEmployeeAsync();
        var newEmail = TestData.UniqueEmail();

        var response = await _client.PutAsJsonAsync($"/api/employees/{created.EmployeeId}",
            TestData.NewEmployee(newEmail, name: "Renamed Person", department: "Sales", experience: 9));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsAsync<EmployeeDto>();
        Assert.Equal(created.EmployeeId, updated.EmployeeId);
        Assert.Equal("Renamed Person", updated.EmployeeName);

        var reloaded = await (await _client.GetAsync($"/api/employees/{created.EmployeeId}")).ReadAsAsync<EmployeeDto>();
        Assert.Equal(newEmail, reloaded.Email);
        Assert.Equal("Sales", reloaded.Department);
        Assert.Equal(9, reloaded.Experience);
    }

    [Fact]
    public async Task Update_KeepingTheSameEmail_IsNotADuplicate()
    {
        var created = await CreateEmployeeAsync();

        var response = await _client.PutAsJsonAsync($"/api/employees/{created.EmployeeId}",
            TestData.NewEmployee(created.Email, name: "Same Email"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToAnotherEmployeesEmail_Returns400()
    {
        var other = await CreateEmployeeAsync();
        var mine = await CreateEmployeeAsync();

        var response = await _client.PutAsJsonAsync($"/api/employees/{mine.EmployeeId}",
            TestData.NewEmployee(other.Email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", (await response.ReadErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Update_WithInvalidData_Returns400_AndLeavesTheEmployeeUnchanged()
    {
        var created = await CreateEmployeeAsync();

        var response = await _client.PutAsJsonAsync($"/api/employees/{created.EmployeeId}",
            TestData.NewEmployee(experience: -3));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reloaded = await (await _client.GetAsync($"/api/employees/{created.EmployeeId}")).ReadAsAsync<EmployeeDto>();
        Assert.Equal(created.Experience, reloaded.Experience);
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/employees/999999", TestData.NewEmployee());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheEmployee()
    {
        var created = await CreateEmployeeAsync();

        var deleteResponse = await _client.DeleteAsync($"/api/employees/{created.EmployeeId}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var message = await deleteResponse.ReadAsAsync<MessageDto>();
        Assert.Contains(created.EmployeeId.ToString(), message.Message);

        var getResponse = await _client.GetAsync($"/api/employees/{created.EmployeeId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenCreateWithSameEmail_Works()
    {
        var created = await CreateEmployeeAsync();
        await _client.DeleteAsync($"/api/employees/{created.EmployeeId}");

        var response = await _client.PostAsJsonAsync("/api/employees", TestData.NewEmployee(created.Email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnknownId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/employees/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Twice_SecondCallReturns404()
    {
        var created = await CreateEmployeeAsync();
        await _client.DeleteAsync($"/api/employees/{created.EmployeeId}");

        var second = await _client.DeleteAsync($"/api/employees/{created.EmployeeId}");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }
}
