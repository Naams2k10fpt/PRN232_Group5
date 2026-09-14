namespace QuizManagement.Infrastructure;

public interface ILoginAttemptService
{
    TimeSpan? GetRemainingLockoutTime(string email, string ipAddress);
}
