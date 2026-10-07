using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace QuizManagement.Tests.ApiTests;

public sealed class Tv3AuthenticationTests : IDisposable
{
    private readonly ApiTestHost _host = new()
    {
        ConfigureServices = services => services.AddSingleton<IDataProtectionProvider>(
            new EphemeralDataProtectionProvider())
    };

    public void Dispose() => _host.Dispose();

    [Theory]
    [InlineData("/api/quiz/attempts/11111111-1111-1111-1111-111111111111?deckId=0")]
    [InlineData("/api/history/user?page=invalid")]
    [InlineData("/api/groups/1/messages?limit=invalid")]
    public async Task ProtectedTv3Api_RequiresAuthentication(string path)
    {
        using var client = _host.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/quiz/attempts/11111111-1111-1111-1111-111111111111?deckId=0")]
    [InlineData("/api/history/user?page=invalid")]
    [InlineData("/api/groups/1/messages?limit=invalid")]
    public async Task ProtectedTv3Api_AcceptsBearerToken(string path)
    {
        _host.AddUser();
        using var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task ChatHubNegotiation_AcceptsBearerToken()
    {
        _host.AddUser();
        using var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var response = await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WebCookie_WorksForChatHubWhileGroupApiRequiresBearer()
    {
        _host.AddUser();
        using var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        var loginResponse = await client.GetAsync("/Account/Login");
        var loginPage = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.IsSuccessStatusCode, loginPage);
        var token = Regex.Match(loginPage,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);

        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "student@example.com",
                ["Password"] = ApiTestHost.Password,
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var groupApi = await client.GetAsync("/api/groups/1/messages?limit=invalid");
        var hub = await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, groupApi.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hub.StatusCode);
    }
}
