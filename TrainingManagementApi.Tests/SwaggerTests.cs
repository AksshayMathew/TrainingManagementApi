using System.Net;
using TrainingManagementApi.Tests.Infrastructure;

namespace TrainingManagementApi.Tests;

public class SwaggerTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public SwaggerTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
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
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadJsonAsync();
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
}
