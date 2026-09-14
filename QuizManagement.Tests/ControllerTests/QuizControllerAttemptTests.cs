using System.Security.Claims;
using System.Text.Json;
using BusinessObjects;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.Quiz;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class QuizControllerAttemptTests
{
    [Fact]
    public void Take_StartsPersistedAttemptThenRefreshesSameAttempt()
    {
        var provider = new EphemeralDataProtectionProvider();
        var attemptId = Guid.NewGuid();
        var quizService = new QuizServiceFake
        {
            StartedAttemptId = attemptId,
            ValidAttempt = new ValidQuizAttempt(
                attemptId,
                [11],
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(30),
                1740)
        };
        var controller = BuildController(
            new DeckServiceFake { Deck = CreateDeck(timeLimitMinutes: 30) },
            quizService,
            provider);

        var postResult = controller.Take(new QuizConfigViewModel { DeckId = 7, QuestionCount = 1 });
        var firstGet = controller.Take(7, attemptId);
        var refreshedGet = controller.Take(7, attemptId);

        var redirect = Assert.IsType<RedirectToActionResult>(postResult);
        Assert.Equal("Take", redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["deckId"]);
        Assert.Equal(attemptId, redirect.RouteValues["attemptId"]);
        var firstModel = Assert.IsType<QuizTakeViewModel>(Assert.IsType<ViewResult>(firstGet).Model);
        var refreshedModel = Assert.IsType<QuizTakeViewModel>(Assert.IsType<ViewResult>(refreshedGet).Model);
        Assert.Equal(7, quizService.StartedDeckId);
        Assert.Equal("user-a", quizService.StartedUserId);
        Assert.Equal([11], quizService.StartedQuestionIds);
        Assert.Equal(30, quizService.StartedTimeLimitMinutes);
        Assert.Equal(1, quizService.StartCalls);
        Assert.Equal(2, quizService.AttemptQuestionLoads);
        Assert.Equal(1740, firstModel.TimeRemainingSeconds);
        Assert.Equal(1740, refreshedModel.TimeRemainingSeconds);
        Assert.NotEmpty(firstModel.AttemptToken);
    }

    [Fact]
    public void Take_RejectsInvalidPersistedDeckTimeWithoutStartingAttempt()
    {
        var quizService = new QuizServiceFake();
        var controller = BuildController(
            new DeckServiceFake { Deck = CreateDeck(timeLimitMinutes: 181) },
            quizService,
            new EphemeralDataProtectionProvider());

        var result = controller.Take(new QuizConfigViewModel { DeckId = 7, QuestionCount = 1 });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Config", view.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, quizService.StartCalls);
    }

    [Fact]
    public void Submit_RedirectsWithoutResultWhenServiceRejectsAttempt()
    {
        var provider = new EphemeralDataProtectionProvider();
        var quizService = new QuizServiceFake { SubmissionResult = null };
        var controller = BuildController(
            new DeckServiceFake { Deck = CreateDeck(timeLimitMinutes: 30) },
            quizService,
            provider);
        var attemptId = Guid.NewGuid();

        var result = controller.Submit(new QuizSubmitViewModel
        {
            DeckId = 7,
            AttemptToken = CreateAttemptToken(provider, attemptId)
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Config", redirect.ActionName);
        Assert.Equal((attemptId, 7, "user-a"), quizService.LastSubmission);
        Assert.Equal(1, quizService.SubmitCalls);
    }

    [Fact]
    public void Submit_DelegatesAttemptIdAndPostedAnswers()
    {
        var provider = new EphemeralDataProtectionProvider();
        var attemptId = Guid.NewGuid();
        var quizService = new QuizServiceFake
        {
            ValidAttempt = new ValidQuizAttempt(
                attemptId,
                [11, 12],
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(30),
                1800),
            SubmissionResult = new TestHistory { Id = 91 }
        };
        var controller = BuildController(
            new DeckServiceFake { Deck = CreateDeck(timeLimitMinutes: 30) },
            quizService,
            provider);

        var result = controller.Submit(new QuizSubmitViewModel
        {
            DeckId = 7,
            AttemptToken = CreateAttemptToken(provider, attemptId),
            Questions =
            [
                new QuizQuestionSubmitItem { QuestionId = 999, SelectedAnswerId = 123 }
            ]
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Result", redirect.ActionName);
        Assert.Equal((attemptId, 7, "user-a"), quizService.LastSubmission);
        Assert.Equal([123], quizService.SubmittedAnswers![999]);
        Assert.Equal(1, quizService.SubmitCalls);
    }

    private static QuizController BuildController(
        IDeckService deckService,
        IQuizService quizService,
        IDataProtectionProvider provider)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-a"),
                new Claim(ClaimTypes.Role, AppRoles.User)
            ], "Test"))
        };

        return new QuizController(deckService, quizService, provider)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TempDataProviderFake())
        };
    }

    private static Deck CreateDeck(int timeLimitMinutes)
    {
        return new Deck
        {
            Id = 7,
            Name = "Deck",
            TimeLimitMinutes = timeLimitMinutes,
            Subject = new Subject { Name = "Subject" }
        };
    }

    private static string CreateAttemptToken(IDataProtectionProvider provider, Guid attemptId)
    {
        var json = JsonSerializer.Serialize(new
        {
            AttemptId = attemptId,
            DeckId = 7,
            UserId = "user-a"
        });
        return provider
            .CreateProtector("QuizManagement.QuizAttempt.v2")
            .Protect(json);
    }

    private sealed class DeckServiceFake : IDeckService
    {
        public Deck? Deck { get; init; }

        public Deck? GetDeckForStudy(int id) => Deck?.Id == id ? Deck : null;
        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotSupportedException();
        public bool TryAddDeck(Deck deck) => throw new NotSupportedException();
        public bool TryUpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }

    private sealed class QuizServiceFake : IQuizService
    {
        public int StartCalls { get; private set; }
        public int StartedDeckId { get; private set; }
        public string? StartedUserId { get; private set; }
        public IReadOnlyList<int>? StartedQuestionIds { get; private set; }
        public int StartedTimeLimitMinutes { get; private set; }
        public Guid StartedAttemptId { get; init; } = Guid.NewGuid();
        public ValidQuizAttempt? ValidAttempt { get; init; }
        public (Guid AttemptId, int DeckId, string UserId)? LastValidation { get; private set; }
        public int AttemptQuestionLoads { get; private set; }
        public TestHistory? SubmissionResult { get; init; }
        public (Guid AttemptId, int DeckId, string UserId)? LastSubmission { get; private set; }
        public int SubmitCalls { get; private set; }
        public IReadOnlyDictionary<int, List<int>>? SubmittedAnswers { get; private set; }

        public List<Question> GetQuestionsForQuiz(int deckId, int questionCount)
        {
            return
            [
                new Question
                {
                    Id = 11,
                    Content = "Question",
                    QuestionType = 1,
                    Answers = [new Answer { Id = 101, Content = "Answer" }]
                }
            ];
        }

        public int GetAvailableQuestionCount(int deckId) => 1;

        public List<Question> GetQuestionsForAttempt(int deckId, IReadOnlyList<int> questionIds)
        {
            AttemptQuestionLoads++;
            return GetQuestionsForQuiz(deckId, questionIds.Count)
                .Where(question => questionIds.Contains(question.Id))
                .ToList();
        }

        public QuizAttempt StartQuizAttempt(
            int deckId,
            string userId,
            IReadOnlyList<int> questionIds,
            int timeLimitMinutes)
        {
            StartCalls++;
            StartedDeckId = deckId;
            StartedUserId = userId;
            StartedQuestionIds = questionIds;
            StartedTimeLimitMinutes = timeLimitMinutes;
            return new QuizAttempt { Id = StartedAttemptId, DeckId = deckId, UserId = userId };
        }

        public ValidQuizAttempt? GetValidQuizAttempt(Guid attemptId, int deckId, string userId)
        {
            LastValidation = (attemptId, deckId, userId);
            return ValidAttempt;
        }

        public TestHistory? SubmitQuizAttempt(
            Guid attemptId,
            int deckId,
            string userId,
            IReadOnlyDictionary<int, List<int>> selectedAnswerIdsByQuestion)
        {
            SubmitCalls++;
            LastSubmission = (attemptId, deckId, userId);
            SubmittedAnswers = selectedAnswerIdsByQuestion;
            return SubmissionResult;
        }

        public TestHistory? GetTestHistoryById(int id, string userId) => throw new NotSupportedException();
        public Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize) => throw new NotSupportedException();
        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId) => throw new NotSupportedException();
        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count) => throw new NotSupportedException();
        public Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByUser(string userId) => throw new NotSupportedException();
        public List<TestHistory> GetRecentTestHistories(string userId, int count) => throw new NotSupportedException();
        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByDeck(int deckId) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin) => throw new NotSupportedException();
    }

    private sealed class TempDataProviderFake : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
