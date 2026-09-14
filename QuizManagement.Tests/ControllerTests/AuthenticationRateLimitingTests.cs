using System.Net;
using System.Reflection;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuizManagement.Controllers;
using QuizManagement.Infrastructure;
using QuizManagement.Tests.TestDoubles;
using QuizManagement.ViewModels.Account;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class AuthenticationRateLimitingTests
{
    [Theory]
    [InlineData(true, 10)]
    [InlineData(false, 5)]
    public async Task FixedWindowPolicy_IsAtomicPerConnectionIp(
        bool loginPolicy,
        int permitLimit)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";

        var partition = loginPolicy
            ? AuthenticationRateLimitPolicies.CreateLoginPartition(context)
            : AuthenticationRateLimitPolicies.CreateRegisterPartition(context);

        Assert.Equal("192.0.2.10", partition.PartitionKey);
        using var limiter = partition.Factory(partition.PartitionKey);
        var leases = await Task.WhenAll(
            Enumerable.Range(0, permitLimit + 3)
                .Select(async _ => await limiter.AcquireAsync(1)));

        try
        {
            Assert.Equal(permitLimit, leases.Count(lease => lease.IsAcquired));
            Assert.Equal(3, leases.Count(lease => !lease.IsAcquired));
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(nameof(AccountController.Login), typeof(LoginViewModel), AuthenticationRateLimitPolicies.Login)]
    [InlineData(nameof(AccountController.Register), typeof(RegisterViewModel), AuthenticationRateLimitPolicies.Register)]
    public void AuthenticationPost_HasItsOwnNamedPolicy(
        string methodName,
        Type modelType,
        string expectedPolicy)
    {
        var method = typeof(AccountController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(candidate =>
                candidate.Name == methodName
                && candidate.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == modelType);

        var attribute = Assert.Single(method.GetCustomAttributes<EnableRateLimitingAttribute>());
        Assert.Equal(expectedPolicy, attribute.PolicyName);
        Assert.NotEqual(AuthenticationRateLimitPolicies.Login, AuthenticationRateLimitPolicies.Register);
    }

    [Fact]
    public async Task Login_UsesConnectionIp_NotAnUntrustedForwardedHeader()
    {
        var attempts = new CapturingLoginAttemptService();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        var controller = new AccountController(
            new ThrowingUserService(),
            attempts,
            new ThrowingLoginAttemptLogService(),
            AccountSecurityFakes.Tokens(),
            new EmailSenderFake())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Login(new LoginViewModel
        {
            Email = "student@test.local",
            Password = "irrelevant"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Equal("192.0.2.10", attempts.LastIpAddress);
    }

    private sealed class CapturingLoginAttemptService : ILoginAttemptService
    {
        public string? LastIpAddress { get; private set; }

        public TimeSpan? GetRemainingLockoutTime(string email, string ipAddress)
        {
            LastIpAddress = ipAddress;
            return TimeSpan.FromMinutes(1);
        }
    }

    private sealed class ThrowingUserService : IUserService
    {
        public User? GetByEmail(string email) => throw new NotSupportedException();
        public User? GetByUsername(string username) => throw new NotSupportedException();
        public User? GetById(string id) => throw new NotSupportedException();
        public bool TryCreateUser(User user) => throw new NotSupportedException();
        public void UpdateProfile(User user) => throw new NotSupportedException();
        public void ChangePassword(User user, string newPasswordHash) => throw new NotSupportedException();
    }

    private sealed class ThrowingLoginAttemptLogService : ILoginAttemptLogService
    {
        public void Log(string email, string ipAddress, bool isSuccess, string? userId = null)
            => throw new NotSupportedException();

        public List<LoginAttempt> GetRecent(int count = 200)
            => throw new NotSupportedException();

        public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success)
            => throw new NotSupportedException();
    }
}
