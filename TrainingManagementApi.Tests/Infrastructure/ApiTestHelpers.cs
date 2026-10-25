using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TrainingManagementApi.Tests.Infrastructure;

public static class ApiTestHelpers
{
    /// <summary>Wraps a raw JSON string as a request body - handy for sending deliberately broken payloads.</summary>
    public static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Reads the <c>errors</c> object of a 400 response as field name -> messages.</summary>
    public static async Task<Dictionary<string, string[]>> ReadErrorsAsync(this HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        Assert.True(json.TryGetProperty("errors", out var errors),
            $"Expected an 'errors' object in the response but got: {json}");

        return errors.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.EnumerateArray().Select(item => item.GetString()!).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A SQLite connection string that can never be opened because its folder does not exist - a stand-in for
    /// "the database is unreachable". It lives under the local temp folder so it never depends on a drive letter
    /// (a mapped network drive that is offline could make a test hang).
    /// </summary>
    public static string UnopenableSqliteConnectionString() =>
        "Data Source=" + Path.Combine(Path.GetTempPath(), "tms-no-such-folder-" + Guid.NewGuid().ToString("N"), "missing.db");

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value!;
    }
}
