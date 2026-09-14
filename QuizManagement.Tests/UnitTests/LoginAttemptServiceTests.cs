using BusinessObjects;
using QuizManagement.Infrastructure;
using Services;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class LoginAttemptServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PersistedFailures_LockAcrossServiceInstances()
    {
        var attempts = Enumerable.Range(0, 5)
            .Select(index => Failure(Now.AddMinutes(-5 + index).UtcDateTime))
            .ToList();
        var logs = new LoginAttemptLogServiceFake(attempts);

        var firstInstance = new LoginAttemptService(logs, new FixedTimeProvider(Now));
        var restartedInstance = new LoginAttemptService(logs, new FixedTimeProvider(Now));

        Assert.InRange(
            firstInstance.GetRemainingLockoutTime("STUDENT@test.local", "192.0.2.10")!.Value,
            TimeSpan.FromMinutes(13),
            TimeSpan.FromMinutes(15));
        Assert.NotNull(restartedInstance.GetRemainingLockoutTime("student@test.local", "192.0.2.10"));
    }

    [Fact]
    public void SuccessOrDifferentIp_DoesNotLockTheCurrentPartition()
    {
        var attempts = Enumerable.Range(0, 4)
            .Select(index => Failure(Now.AddMinutes(-5 + index).UtcDateTime))
            .Append(new LoginAttempt
            {
                Email = "student@test.local",
                IpAddress = "192.0.2.10",
                IsSuccess = true,
                CreatedAt = Now.AddMinutes(-1).UtcDateTime
            })
            .Append(new LoginAttempt
            {
                Email = "student@test.local",
                IpAddress = "192.0.2.11",
                CountsTowardLockout = true,
                CreatedAt = Now.UtcDateTime
            })
            .ToList();
        var service = new LoginAttemptService(
            new LoginAttemptLogServiceFake(attempts),
            new FixedTimeProvider(Now));

        Assert.Null(service.GetRemainingLockoutTime("student@test.local", "192.0.2.10"));
        Assert.Null(service.GetRemainingLockoutTime("student@test.local", "192.0.2.11"));
    }

    private static LoginAttempt Failure(DateTime createdAt) => new()
    {
        Email = "student@test.local",
        IpAddress = "192.0.2.10",
        CountsTowardLockout = true,
        CreatedAt = createdAt
    };

    private sealed class LoginAttemptLogServiceFake(List<LoginAttempt> attempts) : ILoginAttemptLogService
    {
        public List<LoginAttempt> GetRecentForLockout(
            string email,
            string ipAddress,
            DateTime sinceUtc,
            int count)
            => attempts
                .Where(attempt => attempt.Email == email
                    && attempt.IpAddress == ipAddress
                    && attempt.CreatedAt >= sinceUtc)
                .OrderByDescending(attempt => attempt.CreatedAt)
                .Take(count)
                .ToList();

        public void Log(string email, string ipAddress, bool isSuccess, string? userId = null)
            => throw new NotSupportedException();
        public List<LoginAttempt> GetRecent(int count = 200) => throw new NotSupportedException();
        public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
