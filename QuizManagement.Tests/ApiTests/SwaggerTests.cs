using System.Text.Json;
using Xunit;

namespace QuizManagement.Tests.ApiTests;

public class SwaggerTests : IDisposable
{
    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Document_ListsOnlyApiEndpoints_AndMarksOnlyProtectedOnesWithBearer()
    {
        using var document = JsonDocument.Parse(await _host.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");

        Assert.All(paths.EnumerateObject(), path => Assert.StartsWith("/api/", path.Name));
        Assert.True(paths.GetProperty("/api/profile").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/health").GetProperty("get").TryGetProperty("security", out _));

        foreach (var catalogPath in new[] { "/api/subjects", "/api/subjects/{id}", "/api/subjects/{subjectId}/decks", "/api/decks/{id}" })
            Assert.True(paths.GetProperty(catalogPath).GetProperty("get").TryGetProperty("security", out _), catalogPath);
    }
}
