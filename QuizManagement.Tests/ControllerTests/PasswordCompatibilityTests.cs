using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.Controllers;
using QuizManagement.Infrastructure;
using QuizManagement.Tests.TestDoubles;
using QuizManagement.ViewModels.Account;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class PasswordCompatibilityTests
{
    [Fact]
    public async Task Login_AcceptsAnExistingShortPasswordHash()
    {
        var oldPassword = "old123";
        var user = new User
        {
            Id = "legacy-user",
            Username = "legacy-user",
            Email = "legacy@test.local",
            Role = AppRoles.User,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, oldPassword);
        var authentication = new AuthenticationServiceFake();
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var controller = new AccountController(
            new UserServiceFake(user),
            new LoginAttemptServiceFake(),
            new LoginAttemptLogServiceFake(),
            AccountSecurityFakes.Tokens(),
            new EmailSenderFake())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            Url = new UrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
        };

        var result = await controller.Login(new LoginViewModel
        {
            Email = user.Email,
            Password = oldPassword
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.NotNull(authentication.Principal);
        Assert.Equal(user.Id, authentication.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    private sealed class UserServiceFake(User user) : IUserService
    {
        public User? GetByEmail(string email) => email == user.Email ? user : null;
        public User? GetByUsername(string username) => throw new NotSupportedException();
        public User? GetById(string id) => throw new NotSupportedException();
        public bool TryCreateUser(User candidate) => throw new NotSupportedException();
        public void UpdateProfile(User candidate) => throw new NotSupportedException();
        public void ChangePassword(User candidate, string newPasswordHash) => throw new NotSupportedException();
    }

    private sealed class LoginAttemptServiceFake : ILoginAttemptService
    {
        public TimeSpan? GetRemainingLockoutTime(string email, string ipAddress) => null;
    }

    private sealed class LoginAttemptLogServiceFake : ILoginAttemptLogService
    {
        public void Log(string email, string ipAddress, bool isSuccess, string? userId = null) { }
        public List<LoginAttempt> GetRecent(int count = 200) => throw new NotSupportedException();
        public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success) => throw new NotSupportedException();
    }

    private sealed class AuthenticationServiceFake : IAuthenticationService
    {
        public ClaimsPrincipal? Principal { get; private set; }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            Principal = principal;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => throw new NotSupportedException();

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();
    }
}
