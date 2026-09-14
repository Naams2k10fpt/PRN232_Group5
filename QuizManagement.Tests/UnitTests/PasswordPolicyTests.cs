using System.ComponentModel.DataAnnotations;
using QuizManagement.ViewModels.Account;
using QuizManagement.ViewModels.Profile;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Register_ValidatesNewPasswordLength(int length, bool expectedValid)
    {
        var password = new string('a', length);
        var model = new RegisterViewModel
        {
            Username = "student",
            Email = "student@test.local",
            Password = password,
            ConfirmPassword = password
        };

        Assert.Equal(expectedValid, IsValid(model));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    public void ChangePassword_ValidatesOnlyTheNewPasswordPolicy(int length, bool expectedValid)
    {
        var password = new string('a', length);
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old123",
            NewPassword = password,
            ConfirmPassword = password
        };

        Assert.Equal(expectedValid, IsValid(model));
    }

    private static bool IsValid(object model)
        => Validator.TryValidateObject(model, new ValidationContext(model), [], validateAllProperties: true);
}
