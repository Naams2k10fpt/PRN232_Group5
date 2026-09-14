using BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects;

public class LoginAttemptDAO
{
    private static LoginAttemptDAO? _instance;
    private static readonly object _lock = new();

    private LoginAttemptDAO() { }

    public static LoginAttemptDAO Instance
    {
        get { lock (_lock) { return _instance ??= new LoginAttemptDAO(); } }
    }

    public void Log(QuizManagementDbContext context, LoginAttempt attempt)
    {
        context.LoginAttempts.Add(attempt);
        context.SaveChanges();
    }

    public List<LoginAttempt> GetRecent(QuizManagementDbContext context, int count = 200)
    {
        return context.LoginAttempts
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToList();
    }

    public Task<List<LoginAttempt>> GetRecentAsync(
        QuizManagementDbContext context,
        int count,
        bool? success)
    {
        var query = context.LoginAttempts.AsQueryable();
        if (success.HasValue)
        {
            query = query.Where(attempt => attempt.IsSuccess == success.Value);
        }

        return query
            .AsNoTracking()
            .OrderByDescending(attempt => attempt.CreatedAt)
            .Take(Math.Clamp(count, 1, 1000))
            .ToListAsync();
    }

    public List<LoginAttempt> GetRecentForLockout(
        QuizManagementDbContext context,
        string email,
        string ipAddress,
        DateTime sinceUtc,
        int count)
    {
        return context.LoginAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Email == email
                && attempt.IpAddress == ipAddress
                && attempt.CreatedAt >= sinceUtc)
            .OrderByDescending(attempt => attempt.CreatedAt)
            .Take(count)
            .ToList();
    }
}
