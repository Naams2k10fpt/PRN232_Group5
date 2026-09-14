using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.Statistics;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class StatisticsControllerTests
{
    [Fact]
    public async Task Index_MapsDistinctEntityGroupsWithTheSameDisplayName()
    {
        var statistics = new UserStatisticsReadModel
        {
            TotalAttempts = 2,
            SubjectStats =
            [
                new() { Id = 1, Name = "Same subject", Attempts = 1, AveragePercentage = 80 },
                new() { Id = 2, Name = "Same subject", Attempts = 1, AveragePercentage = 60 }
            ],
            DeckStats =
            [
                new() { Id = 10, Name = "Same deck", ParentName = "Same subject", Attempts = 1, AveragePercentage = 80 },
                new() { Id = 20, Name = "Same deck", ParentName = "Same subject", Attempts = 1, AveragePercentage = 60 }
            ]
        };
        var controller = BuildController(new QuizServiceFake { UserStatistics = statistics });

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<StatisticsViewModel>(view.Model);
        Assert.Equal(2, model.SubjectStats.Count);
        Assert.Equal(2, model.DeckStats.Count);
        Assert.All(model.SubjectStats, group => Assert.Equal(1, group.Attempts));
        Assert.All(model.DeckStats, group => Assert.Equal(1, group.Attempts));
    }

    [Fact]
    public async Task MentorStats_MapsGlobalDistinctUsersAndRawAttemptWeightedAverage()
    {
        var statistics = new MentorStatisticsReadModel
        {
            TotalAttempts = 4,
            UniqueUsers = 2,
            AveragePercentage = 25,
            BestPercentage = 100,
            DeckStats =
            [
                new() { Id = 10, Name = "Small deck", ParentName = "Subject", Attempts = 1, UniqueUsers = 1 },
                new() { Id = 20, Name = "Large deck", ParentName = "Subject", Attempts = 3, UniqueUsers = 2 }
            ]
        };
        var controller = BuildController(new QuizServiceFake { MentorStatistics = statistics });

        var result = await controller.MentorStats();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MentorStatsViewModel>(view.Model);
        Assert.Equal(4, model.TotalAttempts);
        Assert.Equal(2, model.UniqueUsers);
        Assert.Equal(25, model.OverallAvgPercentage);
        Assert.Equal(100, model.OverallBestPercentage);
        Assert.Equal(3, model.DeckStats.Sum(deck => deck.UniqueUsers));
    }

    private static StatisticsController BuildController(QuizServiceFake quizService)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "mentor"),
                new Claim(ClaimTypes.Role, AppRoles.Mentor)
            ], "Test"))
        };

        return new StatisticsController(quizService, new DeckServiceFake())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private sealed class QuizServiceFake : IQuizService
    {
        public UserStatisticsReadModel UserStatistics { get; init; } = new();
        public MentorStatisticsReadModel MentorStatistics { get; init; } = new();

        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId) => Task.FromResult(UserStatistics);
        public Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin) => Task.FromResult(MentorStatistics);
        public Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize) => throw new NotSupportedException();
        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByUser(string userId) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin) => throw new NotSupportedException();
        public List<Question> GetQuestionsForQuiz(int deckId, int questionCount) => throw new NotSupportedException();
        public List<Question> GetQuestionsForAttempt(int deckId, IReadOnlyList<int> questionIds) => throw new NotSupportedException();
        public int GetAvailableQuestionCount(int deckId) => throw new NotSupportedException();
        public QuizAttempt StartQuizAttempt(int deckId, string userId, IReadOnlyList<int> questionIds, int timeLimitMinutes) => throw new NotSupportedException();
        public ValidQuizAttempt? GetValidQuizAttempt(Guid attemptId, int deckId, string userId) => throw new NotSupportedException();
        public TestHistory? SubmitQuizAttempt(Guid attemptId, int deckId, string userId, IReadOnlyDictionary<int, List<int>> selectedAnswerIdsByQuestion) => throw new NotSupportedException();
        public TestHistory? GetTestHistoryById(int id, string userId) => throw new NotSupportedException();
        public List<TestHistory> GetRecentTestHistories(string userId, int count) => throw new NotSupportedException();
        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByDeck(int deckId) => throw new NotSupportedException();
    }

    private sealed class DeckServiceFake : IDeckService
    {
        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckForStudy(int id) => throw new NotSupportedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotSupportedException();
        public bool TryAddDeck(Deck deck) => throw new NotSupportedException();
        public bool TryUpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }
}
