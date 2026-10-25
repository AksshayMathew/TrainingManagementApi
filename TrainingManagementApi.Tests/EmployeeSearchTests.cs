using System.Net;
using System.Net.Http.Json;
using TrainingManagementApi.DTOs;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

/// <summary>Bonus features of GET /api/employees: search, department filter, sorting and paging.</summary>
public class EmployeeSearchTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public EmployeeSearchTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<EmployeeDto> CreateAsync(string name, string department, int experience = 1, string joined = "2026-01-15T00:00:00")
    {
        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            employeeName = name,
            email = TestData.UniqueEmail(),
            department,
            joiningDate = joined,
            experience
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<EmployeeDto>();
    }

    private async Task<(List<EmployeeDto> Items, HttpResponseMessage Response)> QueryAsync(string query)
    {
        var response = await _client.GetAsync($"/api/employees?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadAsAsync<List<EmployeeDto>>(), response);
    }

    private static string NewTag() => Guid.NewGuid().ToString("N")[..10];

    // ---------- search ----------

    [Fact]
    public async Task Search_FindsEmployeesByPartOfTheName_IgnoringCase()
    {
        var tag = NewTag();
        var priya = await CreateAsync($"Priya {tag}", "HR");
        await CreateAsync($"Ravi {tag}", "HR");

        var (items, _) = await QueryAsync($"search=pRIYa%20{tag.ToUpperInvariant()}");

        Assert.Equal(priya.EmployeeId, Assert.Single(items).EmployeeId);
    }

    [Fact]
    public async Task Search_FindsEmployeesByDepartment()
    {
        var tag = NewTag();
        var inDepartment = await CreateAsync("Someone", $"Dept-{tag}");
        await CreateAsync("Someone Else", "Another Department");

        var (items, _) = await QueryAsync($"search=dept-{tag}");

        Assert.Equal(inDepartment.EmployeeId, Assert.Single(items).EmployeeId);
    }

    [Fact]
    public async Task Search_WithNoMatch_Returns200_AndAnEmptyList()
    {
        var (items, response) = await QueryAsync($"search=nobody-{NewTag()}");

        Assert.Empty(items);
        Assert.Equal("0", response.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task Search_TreatsPercentAndUnderscoreAsPlainCharacters()
    {
        var tag = NewTag();
        await CreateAsync($"Plain {tag}", "HR");

        var (percent, _) = await QueryAsync("search=%25");        // "%"
        var (underscore, _) = await QueryAsync("search=_");        // "_"

        // Neither is a wildcard: names/departments here contain neither character.
        Assert.DoesNotContain(percent, e => e.EmployeeName.Contains(tag));
        Assert.DoesNotContain(underscore, e => e.EmployeeName.Contains(tag));
    }

    [Fact]
    public async Task Search_WithOnlySpaces_IsIgnored()
    {
        var tag = NewTag();
        await CreateAsync($"Spacey {tag}", "HR");

        var (items, _) = await QueryAsync("search=%20%20%20");

        Assert.Contains(items, e => e.EmployeeName == $"Spacey {tag}");
    }

    // ---------- department filter ----------

    [Fact]
    public async Task DepartmentFilter_MatchesTheWholeDepartmentName_IgnoringCase()
    {
        var tag = NewTag();
        var exact = await CreateAsync("A", $"Ops-{tag}");
        await CreateAsync("B", $"Ops-{tag}-Extra");     // contains the text but is a different department

        var (items, _) = await QueryAsync($"department=OPS-{tag.ToUpperInvariant()}");

        Assert.Equal(exact.EmployeeId, Assert.Single(items).EmployeeId);
    }

    [Fact]
    public async Task DepartmentFilter_CanBeCombinedWithSearch()
    {
        var tag = NewTag();
        var match = await CreateAsync($"Anil {tag}", $"Fin-{tag}");
        await CreateAsync($"Bina {tag}", $"Fin-{tag}");
        await CreateAsync($"Anil {tag}", $"Other-{tag}");

        var (items, _) = await QueryAsync($"department=Fin-{tag}&search=anil");

        Assert.Equal(match.EmployeeId, Assert.Single(items).EmployeeId);
    }

    // ---------- sorting ----------

    [Fact]
    public async Task SortByName_Ascending_AndDescending()
    {
        var tag = NewTag();
        var department = $"Sort-{tag}";
        await CreateAsync("Charlie", department);
        await CreateAsync("Alice", department);
        await CreateAsync("Bob", department);

        var (ascending, _) = await QueryAsync($"department={department}&sortBy=Name");
        var (descending, _) = await QueryAsync($"department={department}&sortBy=name&sortOrder=Desc");

        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, ascending.Select(e => e.EmployeeName));
        Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, descending.Select(e => e.EmployeeName));
    }

    [Fact]
    public async Task SortByExperience_Descending()
    {
        var department = $"Exp-{NewTag()}";
        await CreateAsync("Junior", department, experience: 1);
        await CreateAsync("Senior", department, experience: 20);
        await CreateAsync("Mid", department, experience: 7);

        var (items, _) = await QueryAsync($"department={department}&sortBy=Experience&sortOrder=Desc");

        Assert.Equal(new[] { 20, 7, 1 }, items.Select(e => e.Experience));
    }

    [Fact]
    public async Task SortByJoiningDate_Ascending()
    {
        var department = $"Date-{NewTag()}";
        await CreateAsync("Late", department, joined: "2026-06-01T00:00:00");
        await CreateAsync("Early", department, joined: "2025-01-01T00:00:00");

        var (items, _) = await QueryAsync($"department={department}&sortBy=JoiningDate");

        Assert.Equal(new[] { "Early", "Late" }, items.Select(e => e.EmployeeName));
    }

    [Fact]
    public async Task SortById_Descending_ListsNewestFirst()
    {
        var department = $"Id-{NewTag()}";
        var first = await CreateAsync("First", department);
        var second = await CreateAsync("Second", department);

        var (items, _) = await QueryAsync($"department={department}&sortOrder=Desc");

        Assert.Equal(new[] { second.EmployeeId, first.EmployeeId }, items.Select(e => e.EmployeeId));
    }

    // ---------- paging ----------

    [Fact]
    public async Task Paging_ReturnsTheRequestedPage_AndReportsTotalsInHeaders()
    {
        var department = $"Page-{NewTag()}";
        var created = new List<EmployeeDto>();
        for (var i = 1; i <= 5; i++)
        {
            created.Add(await CreateAsync($"Person {i}", department));
        }

        var (items, response) = await QueryAsync($"department={department}&page=2&pageSize=2");

        Assert.Equal(created.Skip(2).Take(2).Select(e => e.EmployeeId), items.Select(e => e.EmployeeId));
        Assert.Equal("5", response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("2", response.Headers.GetValues("X-Page").Single());
        Assert.Equal("2", response.Headers.GetValues("X-Page-Size").Single());
    }

    [Fact]
    public async Task Paging_TheLastPageCanBePartial_AndPagesBeyondTheEndAreEmpty()
    {
        var department = $"Edge-{NewTag()}";
        for (var i = 1; i <= 5; i++)
        {
            await CreateAsync($"Person {i}", department);
        }

        var (last, _) = await QueryAsync($"department={department}&page=3&pageSize=2");
        var (beyond, beyondResponse) = await QueryAsync($"department={department}&page=4&pageSize=2");

        Assert.Single(last);
        Assert.Empty(beyond);
        Assert.Equal("5", beyondResponse.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task Paging_WithOnlyAPage_UsesTheDefaultPageSizeOf10()
    {
        var department = $"Default-{NewTag()}";
        for (var i = 1; i <= 12; i++)
        {
            await CreateAsync($"Person {i}", department);
        }

        var (items, response) = await QueryAsync($"department={department}&page=1");

        Assert.Equal(10, items.Count);
        Assert.Equal("12", response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("10", response.Headers.GetValues("X-Page-Size").Single());
    }

    [Fact]
    public async Task Paging_KeepsAStableOrder_WhenSortValuesAreEqual()
    {
        var department = $"Stable-{NewTag()}";
        var created = new List<EmployeeDto>();
        for (var i = 1; i <= 4; i++)
        {
            created.Add(await CreateAsync($"Same Name", department));   // identical sort key
        }

        var (page1, _) = await QueryAsync($"department={department}&sortBy=Name&page=1&pageSize=2");
        var (page2, _) = await QueryAsync($"department={department}&sortBy=Name&page=2&pageSize=2");

        Assert.Equal(created.Select(e => e.EmployeeId), page1.Concat(page2).Select(e => e.EmployeeId));
    }

    [Fact]
    public async Task WithoutQueryOptions_ReturnsEveryone_AndStillReportsTheTotal()
    {
        await CreateAsync($"Anyone {NewTag()}", "HR");

        var (items, response) = await QueryAsync(string.Empty);

        Assert.NotEmpty(items);
        Assert.Equal(items.Count.ToString(), response.Headers.GetValues("X-Total-Count").Single());
        Assert.False(response.Headers.Contains("X-Page"));
    }

    // ---------- invalid query options ----------

    [Theory]
    [InlineData("search")]
    [InlineData("department")]
    public async Task OverlongSearchText_Returns400(string option)
    {
        var response = await _client.GetAsync($"/api/employees?{option}={new string('x', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadErrorsAsync()).ContainsKey(option));   // keys are matched ignoring case
    }

    [Theory]
    [InlineData("page=0", "Page")]
    [InlineData("page=-1", "Page")]
    [InlineData("page=1000001", "Page")]
    [InlineData("pageSize=0", "PageSize")]
    [InlineData("pageSize=101", "PageSize")]
    [InlineData("page=abc", "Page")]
    [InlineData("sortBy=salary", "SortBy")]
    [InlineData("sortBy=99", "SortBy")]
    [InlineData("sortOrder=sideways", "SortOrder")]
    public async Task InvalidQueryOptions_Return400_NamingTheBadOption(string query, string expectedField)
    {
        var response = await _client.GetAsync($"/api/employees?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedField, (await response.ReadErrorsAsync()).Keys);
    }
}
