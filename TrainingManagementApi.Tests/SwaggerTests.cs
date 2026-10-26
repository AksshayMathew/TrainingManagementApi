using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

public class SwaggerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SwaggerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> GetDocumentAsync()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    [Fact]
    public async Task SwaggerUi_IsAvailable()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_ListsEveryEmployeeEndpoint()
    {
        var document = await GetDocumentAsync();
        var paths = document.GetProperty("paths");

        var collection = paths.GetProperty("/api/employees");
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(collection.TryGetProperty("post", out _));

        var item = paths.GetProperty("/api/employees/{id}");
        Assert.True(item.TryGetProperty("get", out _));
        Assert.True(item.TryGetProperty("put", out _));
        Assert.True(item.TryGetProperty("delete", out _));
    }

    [Fact]
    public async Task OpenApiDocument_ShowsXmlCommentDescriptions()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var text = await response.Content.ReadAsStringAsync();

        // Comes from the /// comments, proving the XML documentation file is wired into Swagger.
        Assert.Contains("Create a new employee.", text);
    }

    // ---------- usability ----------

    [Fact]
    public async Task Groups_HaveFriendlyNames_InTheOrderAUserNeedsThem_WithDescriptions()
    {
        var document = await GetDocumentAsync();

        var tags = document.GetProperty("tags").EnumerateArray().ToList();
        Assert.Equal(new[] { "Employees", "Courses", "Training assignments" },
            tags.Select(t => t.GetProperty("name").GetString()));
        Assert.All(tags, t => Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("description").GetString())));

        // Every endpoint sits in one of those groups - no leftover technical names such as "TrainingAssignments".
        var used = document.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject())
            .SelectMany(o => o.Value.GetProperty("tags").EnumerateArray().Select(t => t.GetString()))
            .Distinct()
            .ToList();
        Assert.Equal(3, used.Count);
        Assert.DoesNotContain("TrainingAssignments", used);
    }

    [Fact]
    public async Task Employees_AreListedBeforeCoursesAndTraining()
    {
        var document = await GetDocumentAsync();

        var firstPath = document.GetProperty("paths").EnumerateObject().First().Name;

        Assert.Equal("/api/employees", firstPath);
    }

    [Fact]
    public async Task Page_StartsWithAQuickStartGuide()
    {
        var document = await GetDocumentAsync();

        var description = document.GetProperty("info").GetProperty("description").GetString();

        Assert.Contains("How to use this page", description);
        Assert.Contains("POST /api/employees", description);
        Assert.Contains("Reading the result", description);
    }

    [Fact]
    public async Task EveryResponseCode_IsExplainedInPlainWords_NotJustTheHttpName()
    {
        var document = await GetDocumentAsync();

        var assign = document.GetProperty("paths").GetProperty("/api/training-assignments").GetProperty("post").GetProperty("responses");

        Assert.Contains("no employee or no course", assign.GetProperty("404").GetProperty("description").GetString());
        Assert.Contains("already has this course", assign.GetProperty("400").GetProperty("description").GetString());
        Assert.Contains("Assigned", assign.GetProperty("201").GetProperty("description").GetString());
    }

    [Fact]
    public async Task SwaggerPage_EnablesTryItOutByDefault_AndHidesTheTechnicalSchemasSection()
    {
        var html = await (await _client.GetAsync("/swagger/index.html")).Content.ReadAsStringAsync();

        Assert.Contains("\"tryItOutEnabled\":true", html.Replace(" ", string.Empty));
        Assert.Contains("\"defaultModelsExpandDepth\":-1", html.Replace(" ", string.Empty));
        Assert.Contains("\"filter\":\"\"", html.Replace(" ", string.Empty));   // empty expression = filter box on
    }

    [Fact]
    public async Task OpeningJustTheAddress_RedirectsToSwagger_InsteadOfA404()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task TheRootRedirect_IsNotListedAsAnApiEndpoint()
    {
        var document = await GetDocumentAsync();

        Assert.False(document.GetProperty("paths").TryGetProperty("/", out _));
    }
}
