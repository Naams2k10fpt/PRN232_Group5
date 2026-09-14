using BusinessObjects;

namespace Repositories
{
    public interface IQuizRepository
    {
        List<Question> GetQuestionsForQuiz(int deckId);
        int GetQuestionCount(int deckId);
        QuizAttempt AddQuizAttempt(QuizAttempt attempt);
        QuizAttempt? GetQuizAttempt(Guid attemptId, int deckId, string userId);
        TestHistory? GetTestHistoryByQuizAttempt(Guid attemptId, int deckId, string userId);
        TestHistory? CompleteQuizAttempt(
            Guid attemptId,
            int deckId,
            string userId,
            DateTimeOffset completedAtUtc,
            TestHistory history);
        TestHistory? GetTestHistoryById(int id, string userId);
        Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize);
        Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId);
        Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count);
        Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin);
        List<TestHistory> GetTestHistoriesByUser(string userId);
        List<TestHistory> GetRecentTestHistories(string userId, int count);
        (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId);
        List<TestHistory> GetTestHistoriesByDeck(int deckId);
        List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin);
    }
}
