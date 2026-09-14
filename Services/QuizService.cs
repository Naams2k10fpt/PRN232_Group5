using BusinessObjects;
using Repositories;
using System.Text.Json;

namespace Services
{
    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _repository;
        private readonly TimeProvider _timeProvider;
        private static readonly Random _random = new();

        public QuizService(IQuizRepository repository, TimeProvider timeProvider)
        {
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public List<Question> GetQuestionsForQuiz(int deckId, int questionCount)
        {
            var allQuestions = _repository.GetQuestionsForQuiz(deckId);

            Shuffle(allQuestions);

            var selected = allQuestions.Take(questionCount).ToList();

            foreach (var question in selected)
            {
                var answers = question.Answers.ToList();
                Shuffle(answers);
                question.Answers = answers;
            }

            return selected;
        }

        public List<Question> GetQuestionsForAttempt(int deckId, IReadOnlyList<int> questionIds)
        {
            var questionsById = _repository.GetQuestionsForQuiz(deckId)
                .ToDictionary(question => question.Id);
            var selected = questionIds
                .Where(questionsById.ContainsKey)
                .Select(questionId => questionsById[questionId])
                .ToList();

            foreach (var question in selected)
            {
                var answers = question.Answers.ToList();
                Shuffle(answers);
                question.Answers = answers;
            }

            return selected;
        }

        public int GetAvailableQuestionCount(int deckId)
            => _repository.GetQuestionCount(deckId);

        public QuizAttempt StartQuizAttempt(
            int deckId,
            string userId,
            IReadOnlyList<int> questionIds,
            int timeLimitMinutes)
        {
            if (timeLimitMinutes is < 0 or > 180)
                throw new ArgumentOutOfRangeException(nameof(timeLimitMinutes));

            var normalizedQuestionIds = questionIds.Distinct().ToList();
            if (normalizedQuestionIds.Count is < 1 or > 500 || normalizedQuestionIds.Any(id => id <= 0))
                throw new ArgumentException("Danh sách câu hỏi không hợp lệ.", nameof(questionIds));

            var now = _timeProvider.GetUtcNow();
            return _repository.AddQuizAttempt(new QuizAttempt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeckId = deckId,
                QuestionIdsJson = JsonSerializer.Serialize(normalizedQuestionIds),
                TimeLimitMinutes = timeLimitMinutes,
                StartedAtUtc = now,
                ExpiresAtUtc = timeLimitMinutes == 0 ? null : now.AddMinutes(timeLimitMinutes)
            });
        }

        public ValidQuizAttempt? GetValidQuizAttempt(Guid attemptId, int deckId, string userId)
        {
            var attempt = _repository.GetQuizAttempt(attemptId, deckId, userId);
            if (attempt is null || attempt.CompletedAtUtc.HasValue)
                return null;

            if (attempt.TimeLimitMinutes is < 0 or > 180)
                return null;

            var now = _timeProvider.GetUtcNow();
            int? remainingSeconds = null;
            if (attempt.TimeLimitMinutes == 0)
            {
                if (attempt.ExpiresAtUtc.HasValue)
                    return null;
            }
            else
            {
                var expectedExpiry = attempt.StartedAtUtc.AddMinutes(attempt.TimeLimitMinutes);
                if (attempt.ExpiresAtUtc != expectedExpiry
                    || now > expectedExpiry)
                    return null;

                remainingSeconds = Math.Max(
                    0,
                    (int)Math.Ceiling((expectedExpiry - now).TotalSeconds));
            }

            if (string.IsNullOrWhiteSpace(attempt.QuestionIdsJson))
                return null;

            List<int>? questionIds;
            try
            {
                questionIds = JsonSerializer.Deserialize<List<int>>(attempt.QuestionIdsJson);
            }
            catch (JsonException)
            {
                return null;
            }

            if (questionIds is null
                || questionIds.Count is < 1 or > 500
                || questionIds.Any(id => id <= 0)
                || questionIds.Distinct().Count() != questionIds.Count)
                return null;

            return new ValidQuizAttempt(
                attempt.Id,
                questionIds,
                attempt.StartedAtUtc,
                attempt.ExpiresAtUtc,
                remainingSeconds);
        }

        public TestHistory? SubmitQuizAttempt(
            Guid attemptId,
            int deckId,
            string userId,
            IReadOnlyDictionary<int, List<int>> selectedAnswerIdsByQuestion)
        {
            var attempt = GetValidQuizAttempt(attemptId, deckId, userId);
            if (attempt is null)
                return _repository.GetTestHistoryByQuizAttempt(attemptId, deckId, userId);

            var allQuestions = _repository.GetQuestionsForQuiz(deckId);
            var questionDict = allQuestions.ToDictionary(q => q.Id);

            var details = new List<TestResultDetail>();
            var snapshot = new QuizResultSnapshot();
            int correctCount = 0;
            int totalCount = 0;

            foreach (var questionId in attempt.QuestionIds)
            {
                if (!questionDict.TryGetValue(questionId, out var question))
                    continue;

                totalCount++;

                var answerIdsForQuestion = question.Answers
                    .Select(a => a.Id)
                    .ToHashSet();

                var correctAnswerIds = question.Answers
                    .Where(a => a.IsCorrect)
                    .Select(a => a.Id)
                    .ToHashSet();

                var selectedSet = selectedAnswerIdsByQuestion.TryGetValue(questionId, out var selectedAnswerIds)
                    ? selectedAnswerIds.Where(answerIdsForQuestion.Contains).ToHashSet()
                    : new HashSet<int>();

                bool isCorrect;
                if (question.QuestionType == 1)
                {
                    isCorrect = selectedSet.Count == 1 && correctAnswerIds.Contains(selectedSet.First());
                }
                else
                {
                    isCorrect = correctAnswerIds.SetEquals(selectedSet);
                }

                if (isCorrect)
                {
                    correctCount++;
                }

                if (snapshot.Questions.Count == 0)
                {
                    snapshot.DeckName = question.Deck.Name;
                    snapshot.SubjectName = question.Deck.Subject.Name;
                }

                snapshot.Questions.Add(new QuizResultQuestionSnapshot
                {
                    QuestionId = question.Id,
                    Content = question.Content,
                    Explanation = question.Explanation,
                    QuestionType = question.QuestionType,
                    IsCorrect = isCorrect,
                    Answers = question.Answers
                        .OrderBy(answer => answer.Id)
                        .Select(answer => new QuizResultAnswerSnapshot
                        {
                            AnswerId = answer.Id,
                            Content = answer.Content,
                            IsCorrectAnswer = answer.IsCorrect,
                            WasSelected = selectedSet.Contains(answer.Id)
                        })
                        .ToList()
                });

                if (selectedSet.Count == 0)
                {
                    details.Add(new TestResultDetail
                    {
                        QuestionId = questionId,
                        SelectedAnswerId = null,
                        IsCorrect = false
                    });
                }
                else
                {
                    foreach (var answerId in selectedSet)
                    {
                        details.Add(new TestResultDetail
                        {
                            QuestionId = questionId,
                            SelectedAnswerId = answerId,
                            IsCorrect = isCorrect
                        });
                    }
                }
            }

            double percentage = totalCount > 0 ? (double)correctCount / totalCount * 100 : 0;
            double score = totalCount > 0 ? (double)correctCount / totalCount * 10 : 0;

            var completedAtUtc = _timeProvider.GetUtcNow();
            var history = new TestHistory
            {
                UserId = userId,
                DeckId = deckId,
                QuizAttemptId = attemptId,
                ResultSnapshotJson = JsonSerializer.Serialize(snapshot),
                Score = Math.Round(score, 2),
                Percentage = Math.Round(percentage, 2),
                CreatedAt = completedAtUtc.UtcDateTime,
                TestResultDetails = details
            };

            return _repository.CompleteQuizAttempt(
                attemptId,
                deckId,
                userId,
                completedAtUtc,
                history);
        }

        public TestHistory? GetTestHistoryById(int id, string userId)
            => _repository.GetTestHistoryById(id, userId);

        public Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize)
            => _repository.GetTestHistoryPageAsync(userId, page, pageSize);

        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId)
            => _repository.GetUserStatisticsAsync(userId);

        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count)
            => _repository.GetLeaderboardAsync(deckId, count);

        public Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin)
            => _repository.GetMentorStatisticsAsync(ownerUserId, isAdmin);

        public List<TestHistory> GetTestHistoriesByUser(string userId)
            => _repository.GetTestHistoriesByUser(userId);

        public List<TestHistory> GetRecentTestHistories(string userId, int count)
            => _repository.GetRecentTestHistories(userId, count);

        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId)
            => _repository.GetQuizStatistics(userId);

        public List<TestHistory> GetTestHistoriesByDeck(int deckId)
            => _repository.GetTestHistoriesByDeck(deckId);

        public List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin)
            => _repository.GetTestHistoriesByContentOwner(ownerUserId, isAdmin);

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
