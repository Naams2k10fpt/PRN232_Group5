using BusinessObjects;
using DataAccessObjects;

namespace Repositories
{
    public class QuizRepository : IQuizRepository
    {
        private readonly QuizManagementDbContext _context;

        public QuizRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public List<Question> GetQuestionsForQuiz(int deckId)
            => QuizDAO.Instance.GetQuestionsForQuiz(_context, deckId);

        public int GetQuestionCount(int deckId)
            => QuizDAO.Instance.GetQuestionCount(_context, deckId);

        public QuizAttempt AddQuizAttempt(QuizAttempt attempt)
            => QuizDAO.Instance.AddQuizAttempt(_context, attempt);

        public QuizAttempt? GetQuizAttempt(Guid attemptId, int deckId, string userId)
            => QuizDAO.Instance.GetQuizAttempt(_context, attemptId, deckId, userId);

        public TestHistory? GetTestHistoryByQuizAttempt(Guid attemptId, int deckId, string userId)
            => QuizDAO.Instance.GetTestHistoryByQuizAttempt(_context, attemptId, deckId, userId);

        public TestHistory? CompleteQuizAttempt(
            Guid attemptId,
            int deckId,
            string userId,
            DateTimeOffset completedAtUtc,
            TestHistory history)
            => QuizDAO.Instance.CompleteQuizAttempt(
                _context,
                attemptId,
                deckId,
                userId,
                completedAtUtc,
                history);

        public TestHistory? GetTestHistoryById(int id, string userId)
            => QuizDAO.Instance.GetTestHistoryById(_context, id, userId);

        public Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize)
            => QuizDAO.Instance.GetTestHistoryPageAsync(_context, userId, page, pageSize);

        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId)
            => QuizDAO.Instance.GetUserStatisticsAsync(_context, userId);

        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count)
            => QuizDAO.Instance.GetLeaderboardAsync(_context, deckId, count);

        public Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin)
            => QuizDAO.Instance.GetMentorStatisticsAsync(_context, ownerUserId, isAdmin);

        public List<TestHistory> GetTestHistoriesByUser(string userId)
            => QuizDAO.Instance.GetTestHistoriesByUser(_context, userId);

        public List<TestHistory> GetRecentTestHistories(string userId, int count)
            => QuizDAO.Instance.GetRecentTestHistories(_context, userId, count);

        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId)
            => QuizDAO.Instance.GetQuizStatistics(_context, userId);

        public List<TestHistory> GetTestHistoriesByDeck(int deckId)
            => QuizDAO.Instance.GetTestHistoriesByDeck(_context, deckId);

        public List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin)
            => QuizDAO.Instance.GetTestHistoriesByContentOwner(_context, ownerUserId, isAdmin);
    }
}
