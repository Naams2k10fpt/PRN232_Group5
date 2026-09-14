using BusinessObjects;

namespace Repositories;

public interface ILoginAttemptRepository
{
    void Log(LoginAttempt attempt);
    List<LoginAttempt> GetRecent(int count = 200);
    Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success);
    List<LoginAttempt> GetRecentForLockout(string email, string ipAddress, DateTime sinceUtc, int count);
}
