using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace QuizManagement.Tests.ApiTests;

public sealed class AppUpdatesApiTests
{
    [Theory]
    [InlineData("https://example.com/quizms.apk", HttpStatusCode.OK)]
    [InlineData("http://example.com/quizms.apk", HttpStatusCode.NoContent)]
    [InlineData(null, HttpStatusCode.NoContent)]
    public async Task Android_ReturnsOnlyPublishedHttpsReleaseWithoutLogin(string? url, HttpStatusCode expected)
    {
        using var host = new ApiTestHost
        {
            ConfigureServices = services => services.AddSingleton<IConfiguration>(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AppUpdates:Android:VersionName"] = "1.1.0",
                    ["AppUpdates:Android:VersionCode"] = "2003",
                    ["AppUpdates:Android:Abi"] = "arm64-v8a",
                    ["AppUpdates:Android:UpdateUrl"] = url
                }).Build())
        };
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/app-updates/android");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("1.1.0", body.RootElement.GetProperty("versionName").GetString());
            Assert.Equal(2003, body.RootElement.GetProperty("versionCode").GetInt32());
            Assert.Equal(url, body.RootElement.GetProperty("updateUrl").GetString());
        }
    }
}
