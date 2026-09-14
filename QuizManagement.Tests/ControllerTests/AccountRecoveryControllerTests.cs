using BusinessObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuizManagement.Controllers;
using QuizManagement.Infrastructure;
using QuizManagement.Tests.TestDoubles;
using QuizManagement.ViewModels.Account;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class AccountRecoveryControllerTests
{
    [Fact]
    public void VerifyEmail_ConfirmsUserAndInvalidatesTheToken()
    {
        var user = User();
        var tokens = AccountSecurityFakes.Tokens();
        var token = tokens.CreateEmailVerificationToken(user);
        var controller = Controller(user, tokens);

        var result = controller.VerifyEmail(user.Id, token);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(user.EmailConfirmed);
        Assert.False(tokens.ValidateEmailVerificationToken(user, token));
    }

    [Fact]
    public void ResetPassword_ChangesHashAndInvalidatesTheToken()
    {
        var user = User();
        user.EmailConfirmed = true;
        var tokens = AccountSecurityFakes.Tokens();
        var token = tokens.CreatePasswordResetToken(user);
        var controller = Controller(user, tokens);

        var result = controller.ResetPassword(new ResetPasswordViewModel
        {
            UserId = user.Id,
            Token = token,
            Password = "a new sufficiently long passphrase",
            ConfirmPassword = "a new sufficiently long passphrase"
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(
                user,
                user.PasswordHash!,
                "a new sufficiently long passphrase"));
        Assert.False(tokens.ValidatePasswordResetToken(user, token));
    }

    private static AccountController Controller(User user, AccountTokenService tokens)
    {
        var controller = new AccountController(
            new UserServiceFake(user),
            new LoginAttemptServiceFake(),
            new LoginAttemptLogServiceFake(),
            tokens,
            new EmailSenderFake());
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            new TempDataProviderFake());
        return controller;
    }

    private static User User() => new()
    {
        Id = "user",
        Username = "user",
        Email = "user@test.local",
        SecurityStamp = "initial"
    };

    private sealed class UserServiceFake(User user) : IUserService
    {
        public User? GetById(string id) => id == user.Id ? user : null;
        public void UpdateProfile(User candidate) { }
        public void ChangePassword(User candidate, string newPasswordHash)
        {
            candidate.PasswordHash = newPasswordHash;
            candidate.SecurityStamp = Guid.NewGuid().ToString();
        }

        public User? GetByEmail(string email) => throw new NotSupportedException();
        public User? GetByUsername(string username) => throw new NotSupportedException();
        public bool TryCreateUser(User candidate) => throw new NotSupportedException();
    }

    private sealed class LoginAttemptServiceFake : ILoginAttemptService
    {
        public TimeSpan? GetRemainingLockoutTime(string email, string ipAddress) => null;
    }

    private sealed class LoginAttemptLogServiceFake : ILoginAttemptLogService
    {
        public void Log(string email, string ipAddress, bool isSuccess, string? userId = null) { }
        public List<LoginAttempt> GetRecent(int count = 200) => throw new NotSupportedException();
        public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success)
            => throw new NotSupportedException();
    }

    private sealed class TempDataProviderFake : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
