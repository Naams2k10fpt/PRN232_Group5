using System.Net;
using System.Net.Http.Json;
using QuizManagement.ApiModels;
using Xunit;
using static QuizManagement.Tests.ApiTests.AuthApiTests;

namespace QuizManagement.Tests.ApiTests;

public class ProfileApiTests : IDisposable
{
    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Get_WithToken_ReturnsCurrentUser()
    {
        var user = _host.AddUser();
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var profile = await client.GetFromJsonAsync<ApiUser>("/api/profile");

        Assert.Equal(user.Id, profile!.Id);
        Assert.Equal(user.Email, profile.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ4In0.c2lnbmF0dXJl")]
    public async Task Get_WithoutValidToken_Returns401Json_NotALoginRedirect(string? token)
    {
        var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        if (token is not null)
            ApiTestHost.UseToken(client, token);

        await AssertError(await client.GetAsync("/api/profile"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Token_StopsWorkingWhenTheUserIsDisabled()
    {
        var user = _host.AddUser();
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        user.IsDisabled = true;

        await AssertError(await client.GetAsync("/api/profile"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task BearerToken_DoesNotSignInToTheWebPages()
    {
        _host.AddUser();
        var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var response = await client.GetAsync("/Profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Update_ChangesUsernameAndAvatar()
    {
        var user = _host.AddUser();
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var response = await client.PutAsJsonAsync("/api/profile",
            new { username = "  renamed ", avatarUrl = "https://example.com/a.png" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("renamed", user.Username);
        Assert.Equal("https://example.com/a.png", user.AvatarUrl);
        Assert.Equal("renamed", (await response.Content.ReadFromJsonAsync<ApiUser>())!.Username);
    }

    [Fact]
    public async Task Update_UsernameTakenByAnotherUser_Returns400OnUsername()
    {
        var user = _host.AddUser();
        _host.AddUser("taken@example.com");
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var error = await AssertError(
            await client.PutAsJsonAsync("/api/profile", new { username = "TAKEN" }),
            HttpStatusCode.BadRequest, "validation_failed");

        Assert.Contains("username", error.Errors!.Keys);
        Assert.Equal("student", user.Username);
    }

    [Fact]
    public async Task ChangePassword_ReturnsANewTokenAndRevokesTheOldOne()
    {
        _host.AddUser();
        var client = _host.CreateClient();
        var oldToken = await _host.LoginAsync(client);
        ApiTestHost.UseToken(client, oldToken);

        var response = await client.PostAsJsonAsync("/api/profile/change-password", new
        {
            currentPassword = ApiTestHost.Password,
            newPassword = "a brand new long passphrase",
            confirmPassword = "a brand new long passphrase"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newToken = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken;

        await AssertError(await client.GetAsync("/api/profile"), HttpStatusCode.Unauthorized, "unauthorized");
        ApiTestHost.UseToken(client, newToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400OnCurrentPassword()
    {
        _host.AddUser();
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));

        var error = await AssertError(
            await client.PostAsJsonAsync("/api/profile/change-password", new
            {
                currentPassword = "not my password",
                newPassword = "a brand new long passphrase",
                confirmPassword = "a brand new long passphrase"
            }),
            HttpStatusCode.BadRequest, "validation_failed");

        Assert.Contains("currentPassword", error.Errors!.Keys);
    }
}
