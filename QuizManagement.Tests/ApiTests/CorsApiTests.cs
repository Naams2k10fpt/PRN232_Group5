using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.ApiModels;
using Xunit;

namespace QuizManagement.Tests.ApiTests;

[CollectionDefinition("Hosting configuration", DisableParallelization = true)]
public sealed class HostingConfigurationCollection;

[Collection("Hosting configuration")]
public sealed class CorsApiTests
{
    [Fact]
    public async Task Development_WithHttpsPort_AllowsHttpPreflightAndLoginFromFlutter()
    {
        const string origin = "http://localhost:49471";
        using var host = new ApiTestHost();
        host.AddUser();
        // Visual Studio's https profile exposes both 5039 and 7133. TestServer does not
        // have Kestrel addresses, so supply the same HTTPS port explicitly.
        using var configured = host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("https_port", "7133");
            builder.ConfigureTestServices(services => services.AddSingleton<IDataProtectionProvider>(
                new EphemeralDataProtectionProvider()));
        });
        using var client = configured.CreateClient(new() { AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost:5039") });
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var allowed = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Null(allowed.Headers.Location);
        Assert.Equal(origin, Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", allowed.Headers.GetValues("Access-Control-Allow-Methods"));
        Assert.Contains("content-type", allowed.Headers.GetValues("Access-Control-Allow-Headers"));

        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "student@example.com", password = ApiTestHost.Password })
        };
        login.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.AccessToken));
    }

    [Fact]
    public async Task Production_WithoutProxy_StillRedirectsHttpToHttps()
    {
        using var host = new ApiTestHost();
        // Program validates local/env configuration before WebApplicationFactory's
        // ConfigureAppConfiguration callback. Isolate these process-wide overrides
        // from other tests through the non-parallel collection and restore them.
        var settings = new Dictionary<string, string?>
        {
            ["App__PublicBaseUrl"] = "https://quiz.example.test",
            ["Jwt__SigningKey"] = "only-a-test-key-with-at-least-32-bytes",
            ["Email__Provider"] = "Smtp",
            ["Email__Host"] = "smtp.example.test",
            ["Hosting__BehindProxy"] = "false"
        };
        var originals = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var setting in settings) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            using var configured = host.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("https_port", "7133");
                builder.ConfigureTestServices(services => services.AddSingleton<IDataProtectionProvider>(
                    new EphemeralDataProtectionProvider()));
            });
            using var client = configured.CreateClient(new() { AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost:5039") });
            using var response = await client.GetAsync("/api/auth/login");
            Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
            Assert.Equal("https://localhost:7133/api/auth/login", response.Headers.Location?.ToString());
        }
        finally
        {
            foreach (var setting in originals) Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }
}
