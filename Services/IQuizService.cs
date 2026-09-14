using BusinessObjects;

namespace Services
{
    public sealed record ValidQuizAttempt(
        Guid Id,
        IReadOnlyList<int> QuestionIds,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        int? RemainingSeconds);

    public interface IQuizService
    {
        List<Question> GetQuestionsForQuiz(int deckId, int questionCount);
        List<Question> GetQuestionsForAttempt(int deckId, IReadOnlyList<int> questionIds);
        int GetAvailableQuestionCount(int deckId);
        QuizAttempt StartQuizAttempt(
            int deckId,
            string userId,
            IReadOnlyList<int> questionIds,
            int timeLimitMinutes);
        ValidQuizAttempt? GetValidQuizAttempt(Guid attemptId, int deckId, string userId);
        TestHistory? SubmitQuizAttempt(
            Guid attemptId,
            int deckId,
            string userId,
            IReadOnlyDictionary<int, List<int>> selectedAnswerIdsByQuestion);
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
