using Services;

namespace QuizManagement.Infrastructure;

public class LoginAttemptService : ILoginAttemptService
{
    private readonly ILoginAttemptLogService _loginAttemptLogService;
    private readonly TimeProvider _timeProvider;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public LoginAttemptService(
        ILoginAttemptLogService loginAttemptLogService,
        TimeProvider timeProvider)
    {
        _loginAttemptLogService = loginAttemptLogService;
        _timeProvider = timeProvider;
    }

    public TimeSpan? GetRemainingLockoutTime(string email, string ipAddress)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var attempts = _loginAttemptLogService.GetRecentForLockout(
            email.Trim().ToLowerInvariant(),
            ipAddress,
            now - LockoutDuration,
            MaxFailedAttempts);
        if (attempts.Count < MaxFailedAttempts || attempts.Any(attempt => !attempt.CountsTowardLockout))
            return null;

        var remaining = attempts.Max(attempt => attempt.CreatedAt) + LockoutDuration - now;
        return remaining > TimeSpan.Zero ? remaining : null;
    }
}
