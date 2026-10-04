using System.Net;
using System.Net.Http.Json;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Xunit;

namespace QuizManagement.Tests.ApiTests;

public class AuthApiTests : IDisposable
{
    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Login_ReturnsTokenAndUser()
    {
        var user = _host.AddUser();
        var client = _host.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "  Student@Example.com ", password = ApiTestHost.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        Assert.Equal(user.Id, body.User.Id);
        Assert.Equal("User", body.User.Role);
    }

    [Theory]
    [InlineData("student@example.com", "wrong password", "invalid_credentials", HttpStatusCode.Unauthorized)]
    [InlineData("nobody@example.com", ApiTestHost.Password, "invalid_credentials", HttpStatusCode.Unauthorized)]
    public async Task Login_WrongCredentials_Returns401(string email, string password, string code, HttpStatusCode status)
    {
        _host.AddUser();

        var response = await _host.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

        await AssertError(response, status, code);
    }

    [Fact]
    public async Task Login_DisabledAndUnconfirmedUsers_Return403WithTheirCode()
    {
        _host.AddUser("disabled@example.com", u => u.IsDisabled = true);
        _host.AddUser("unconfirmed@example.com", u => u.EmailConfirmed = false);
        var client = _host.CreateClient();

        await AssertError(
            await client.PostAsJsonAsync("/api/auth/login", new { email = "disabled@example.com", password = ApiTestHost.Password }),
            HttpStatusCode.Forbidden, "account_disabled");
        await AssertError(
            await client.PostAsJsonAsync("/api/auth/login", new { email = "unconfirmed@example.com", password = ApiTestHost.Password }),
            HttpStatusCode.Forbidden, "email_not_confirmed");
    }

    [Fact]
    public async Task Login_LockedOut_Returns429WithRetryAfter()
    {
        _host.AddUser();
        _host.Lockout.Remaining = TimeSpan.FromSeconds(90);

        var response = await _host.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "student@example.com", password = ApiTestHost.Password });

        var error = await AssertError(response, HttpStatusCode.TooManyRequests, "locked_out");
        Assert.Equal(90, error.RetryAfterSeconds);
        Assert.Equal("90", response.Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task Login_InvalidBody_ReturnsValidationErrorsByField()
    {
        var response = await _host.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" });

        var error = await AssertError(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Contains("email", error.Errors!.Keys);
        Assert.Contains("password", error.Errors!.Keys);
    }

    [Fact]
    public async Task Register_CreatesUnconfirmedUserAndSendsVerificationLinkToTheWebPage()
    {
        var response = await _host.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            username = "newbie",
            email = "newbie@example.com",
            password = ApiTestHost.Password,
            confirmPassword = ApiTestHost.Password
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = _host.Users.GetByEmail("newbie@example.com");
        Assert.NotNull(user);
        Assert.False(user!.EmailConfirmed);
        var email = Assert.Single(_host.Emails.Messages);
        Assert.Contains("/Account/VerifyEmail?userId=", email.Body);
    }

    [Fact]
    public async Task Register_TakenEmail_Returns409WithoutSayingWhichFieldClashed()
    {
        _host.AddUser();

        var response = await _host.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            username = "someone-else",
            email = "student@example.com",
            password = ApiTestHost.Password,
            confirmPassword = ApiTestHost.Password
        });

        var error = await AssertError(response, HttpStatusCode.Conflict, "registration_conflict");
        Assert.Null(error.Errors);
    }

    [Fact]
    public async Task Register_MismatchedConfirmation_Returns400()
    {
        var response = await _host.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            username = "newbie",
            email = "newbie@example.com",
            password = ApiTestHost.Password,
            confirmPassword = "something else entirely"
        });

        var error = await AssertError(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Contains("confirmPassword", error.Errors!.Keys);
    }

    [Fact]
    public async Task ResendVerificationAndForgotPassword_AlwaysAccept_AndOnlyEmailEligibleUsers()
    {
        _host.AddUser("unconfirmed@example.com", u => u.EmailConfirmed = false);
        _host.AddUser("confirmed@example.com");
        var client = _host.CreateClient();

        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsJsonAsync("/api/auth/resend-verification", new { email = "unconfirmed@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsJsonAsync("/api/auth/resend-verification", new { email = "confirmed@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "confirmed@example.com" })).StatusCode);

        Assert.Collection(_host.Emails.Messages,
            m => Assert.Equal("unconfirmed@example.com", m.Recipient),
            m => { Assert.Equal("confirmed@example.com", m.Recipient); Assert.Contains("/Account/ResetPassword?userId=", m.Body); });
    }

    [Fact]
    public async Task RegisterPolicy_RateLimitsApiCallsWithJsonError()
    {
        var client = _host.CreateClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
            last = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });

        await AssertError(last!, HttpStatusCode.TooManyRequests, "rate_limited");
    }

    internal static async Task<ApiError> AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal(code, error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        return error;
    }
}
