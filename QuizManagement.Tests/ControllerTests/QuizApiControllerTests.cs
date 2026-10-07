using System.Security.Claims;
using System.Text.Json;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Controllers;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public sealed class QuizApiControllerTests
{
    [Fact]
    public void Start_ReturnsAttemptWithoutCorrectAnswerFlags()
    {
        var quiz = new QuizServiceFake();
        var controller = new QuizApiController(new DeckServiceFake(), quiz)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "user-a")], "Test"))
                }
            }
        };

        var result = Assert.IsType<OkObjectResult>(controller.Start(new StartQuizRequest(7, 1)));
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"attemptId\"", json);
        Assert.Contains("\"questionId\":11", json);
        Assert.Contains("\"answerId\":21", json);
        Assert.DoesNotContain("isCorrect", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("user-a", quiz.StartedUserId);
        Assert.Equal(new[] { 11 }, quiz.StartedQuestionIds);
    }

    [Fact]
    public void Submit_RejectsMalformedAnswersBeforeCallingService()
    {
        var controller = new QuizApiController(new DeckServiceFake(), new QuizServiceFake());

        Assert.IsType<BadRequestObjectResult>(controller.Submit(
            new SubmitQuizRequest(Guid.NewGuid(), 7, null)));
        Assert.IsType<BadRequestObjectResult>(controller.Submit(
            new SubmitQuizRequest(Guid.NewGuid(), 7,
                [new QuizAnswerRequest(-1, [21])])));
        Assert.IsType<BadRequestObjectResult>(controller.Submit(
            new SubmitQuizRequest(Guid.NewGuid(), 7,
                [new QuizAnswerRequest(11, [-2])])));
    }

    [Fact]
    public void Start_RejectsMoreThanFiveHundredQuestionsBeforeCreatingAttempt()
    {
        var quiz = new QuizServiceFake { AvailableQuestionCount = 501 };
        var controller = new QuizApiController(new DeckServiceFake(), quiz);

        Assert.IsType<BadRequestObjectResult>(controller.Start(new StartQuizRequest(7, 501)));
        Assert.Null(quiz.StartedUserId);
    }

    private sealed class DeckServiceFake : IDeckService
    {
        public Deck? GetDeckForStudy(int id) => new()
        {
            Id = id, Name = "Bộ đề mẫu", TimeLimitMinutes = 15,
            Subject = new Subject { Id = 1, Name = "Môn mẫu", UserId = "owner" }
        };
        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotImplementedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotImplementedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotImplementedException();
        public bool TryAddDeck(Deck deck) => throw new NotImplementedException();
        public bool TryUpdateDeck(Deck deck) => throw new NotImplementedException();
        public void DeleteDeck(Deck deck) => throw new NotImplementedException();
    }

    private sealed class QuizServiceFake : IQuizService
    {
        public int AvailableQuestionCount { get; init; } = 1;
        public string? StartedUserId { get; private set; }
        public IReadOnlyList<int>? StartedQuestionIds { get; private set; }
        public int GetAvailableQuestionCount(int deckId) => AvailableQuestionCount;
        public List<Question> GetQuestionsForQuiz(int deckId, int questionCount) =>
        [
            new Question
            {
                Id = 11, DeckId = deckId, Content = "Câu hỏi", QuestionType = 1,
                Answers = [new Answer { Id = 21, Content = "Đáp án", IsCorrect = true }]
            }
        ];
        public QuizAttempt StartQuizAttempt(int deckId, string userId,
            IReadOnlyList<int> questionIds, int timeLimitMinutes)
        {
            StartedUserId = userId;
            StartedQuestionIds = questionIds;
            return new QuizAttempt
            {
                Id = Guid.NewGuid(), DeckId = deckId, UserId = userId,
                QuestionIdsJson = "[11]", StartedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(timeLimitMinutes)
            };
        }
        public List<Question> GetQuestionsForAttempt(int deckId, IReadOnlyList<int> questionIds) => throw new NotImplementedException();
        public ValidQuizAttempt? GetValidQuizAttempt(Guid attemptId, int deckId, string userId) => throw new NotImplementedException();
        public TestHistory? SubmitQuizAttempt(Guid attemptId, int deckId, string userId,
            IReadOnlyDictionary<int, List<int>> selectedAnswerIdsByQuestion) => throw new NotImplementedException();
        public TestHistory? GetTestHistoryById(int id, string userId) => throw new NotImplementedException();
        public Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize) => throw new NotImplementedException();
        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId) => throw new NotImplementedException();
        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count) => throw new NotImplementedException();
        public Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin) => throw new NotImplementedException();
        public List<TestHistory> GetTestHistoriesByUser(string userId) => throw new NotImplementedException();
        public List<TestHistory> GetRecentTestHistories(string userId, int count) => throw new NotImplementedException();
        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId) => throw new NotImplementedException();
        public List<TestHistory> GetTestHistoriesByDeck(int deckId) => throw new NotImplementedException();
        public List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin) => throw new NotImplementedException();
    }
}
