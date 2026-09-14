using BusinessObjects;
using Microsoft.AspNetCore.DataProtection;
using QuizManagement.Infrastructure;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class AccountTokenServiceTests
{
    [Fact]
    public void Tokens_ArePurposeBoundAndInvalidatedBySecurityStampChange()
    {
        var service = new AccountTokenService(new EphemeralDataProtectionProvider());
        var user = new User { Id = "user", SecurityStamp = "first" };
        var verification = service.CreateEmailVerificationToken(user);
        var reset = service.CreatePasswordResetToken(user);

        Assert.True(service.ValidateEmailVerificationToken(user, verification));
        Assert.True(service.ValidatePasswordResetToken(user, reset));
        Assert.False(service.ValidatePasswordResetToken(user, verification));

        user.SecurityStamp = "second";
        Assert.False(service.ValidateEmailVerificationToken(user, verification));
        Assert.False(service.ValidatePasswordResetToken(user, reset));
    }
}
