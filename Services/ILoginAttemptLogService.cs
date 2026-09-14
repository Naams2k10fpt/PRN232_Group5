namespace Services;

public interface ILoginAttemptLogService
{
    void Log(string email, string ipAddress, bool isSuccess, string? userId = null);
    void Log(
        string email,
        string ipAddress,
        bool isSuccess,
        string? userId,
        bool countsTowardLockout)
        => Log(email, ipAddress, isSuccess, userId);
    List<BusinessObjects.LoginAttempt> GetRecent(int count = 200);
    Task<List<BusinessObjects.LoginAttempt>> GetRecentAsync(int count, bool? success);
    List<BusinessObjects.LoginAttempt> GetRecentForLockout(
        string email,
        string ipAddress,
        DateTime sinceUtc,
        int count)
        => throw new NotSupportedException();
}
