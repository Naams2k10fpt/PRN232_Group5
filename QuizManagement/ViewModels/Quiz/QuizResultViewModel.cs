using BusinessObjects;
using System.Text.Json;

namespace QuizManagement.ViewModels.Quiz
{
    /// <summary>
    /// ViewModel hiển thị kết quả sau khi nộp bài hoặc xem lại lịch sử
    /// </summary>
    public class QuizResultViewModel
    {
        public int TestHistoryId { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public double Score { get; set; }

        public double Percentage { get; set; }

        public int CorrectCount { get; set; }

        public int TotalCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<QuizResultQuestionViewModel> Questions { get; set; } = new();

        public static QuizResultViewModel FromHistory(TestHistory history)
        {
            var snapshot = ReadSnapshot(history.ResultSnapshotJson);
            if (snapshot is not null)
            {
                var questions = snapshot.Questions.Select(question => new QuizResultQuestionViewModel
                {
                    QuestionId = question.QuestionId,
                    Content = question.Content,
                    Explanation = question.Explanation,
                    QuestionType = question.QuestionType,
                    IsCorrect = question.IsCorrect,
                    Answers = question.Answers.Select(answer => new QuizResultAnswerViewModel
                    {
                        AnswerId = answer.AnswerId,
                        Content = answer.Content,
                        IsCorrectAnswer = answer.IsCorrectAnswer,
                        WasSelected = answer.WasSelected
                    }).ToList()
                }).ToList();

                return Create(
                    history,
                    snapshot.DeckName,
                    snapshot.SubjectName,
                    questions);
            }

            var questionGroups = history.TestResultDetails.GroupBy(detail => detail.QuestionId);
            var legacyQuestions = questionGroups.Select(group =>
            {
                var firstDetail = group.First();
                var question = firstDetail.Question;
                var selectedAnswerIds = group
                    .Where(detail => detail.SelectedAnswerId.HasValue)
                    .Select(detail => detail.SelectedAnswerId!.Value)
                    .ToHashSet();

                return new QuizResultQuestionViewModel
                {
                    QuestionId = question.Id,
                    Content = question.Content,
                    Explanation = question.Explanation,
                    QuestionType = question.QuestionType,
                    IsCorrect = firstDetail.IsCorrect,
                    Answers = question.Answers
                        .OrderBy(answer => answer.Id)
                        .Select(answer => new QuizResultAnswerViewModel
                        {
                            AnswerId = answer.Id,
                            Content = answer.Content,
                            IsCorrectAnswer = answer.IsCorrect,
                            WasSelected = selectedAnswerIds.Contains(answer.Id)
                        }).ToList()
                };
            }).ToList();

            return Create(history, history.Deck.Name, history.Deck.Subject.Name, legacyQuestions);
        }

        private static QuizResultSnapshot? ReadSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var snapshot = JsonSerializer.Deserialize<QuizResultSnapshot>(json);
                return snapshot is not null
                    && !string.IsNullOrWhiteSpace(snapshot.DeckName)
                    && !string.IsNullOrWhiteSpace(snapshot.SubjectName)
                    && snapshot.Questions is { Count: > 0 }
                    && snapshot.Questions.All(question => question is not null && question.Answers is not null)
                    ? snapshot
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static QuizResultViewModel Create(
            TestHistory history,
            string deckName,
            string subjectName,
            List<QuizResultQuestionViewModel> questions)
        {
            return new QuizResultViewModel
            {
                TestHistoryId = history.Id,
                DeckName = deckName,
                SubjectName = subjectName,
                Score = history.Score,
                Percentage = history.Percentage,
                CorrectCount = questions.Count(question => question.IsCorrect),
                TotalCount = questions.Count,
                CreatedAt = history.CreatedAt,
                Questions = questions
            };
        }
    }

    public class QuizResultQuestionViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public string? Explanation { get; set; }

        public int QuestionType { get; set; }

        public bool IsCorrect { get; set; }

        public List<QuizResultAnswerViewModel> Answers { get; set; } = new();
    }

    public class QuizResultAnswerViewModel
    {
        public int AnswerId { get; set; }

        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Đáp án này là đáp án đúng
        /// </summary>
        public bool IsCorrectAnswer { get; set; }

        /// <summary>
        /// Người dùng đã chọn đáp án này
        /// </summary>
        public bool WasSelected { get; set; }
    }
}
