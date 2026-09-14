using BusinessObjects;
using Repositories;

namespace Services;

public class LoginAttemptLogService : ILoginAttemptLogService
{
    private readonly ILoginAttemptRepository _repository;

    public LoginAttemptLogService(ILoginAttemptRepository repository)
    {
        _repository = repository;
    }

    public void Log(
        string email,
        string ipAddress,
        bool isSuccess,
        string? userId = null)
        => Log(email, ipAddress, isSuccess, userId, false);

    public void Log(
        string email,
        string ipAddress,
        bool isSuccess,
        string? userId,
        bool countsTowardLockout)
    {
        _repository.Log(new LoginAttempt
        {
            Email = email.Trim().ToLowerInvariant(),
            IpAddress = ipAddress,
            IsSuccess = isSuccess,
            CountsTowardLockout = countsTowardLockout,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });
    }

    public List<LoginAttempt> GetRecent(int count = 200) => _repository.GetRecent(count);
    public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success)
        => _repository.GetRecentAsync(count, success);
    public List<LoginAttempt> GetRecentForLockout(
        string email,
        string ipAddress,
        DateTime sinceUtc,
        int count)
        => _repository.GetRecentForLockout(email, ipAddress, sinceUtc, count);
}
