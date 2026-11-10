using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.Controllers;
using QuizManagement.Tests.ApiTests;
using QuizManagement.ViewModels.Statistics;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class StatisticsControllerTests
{
    [Fact]
    public async Task StatisticsApi_UsesSignedInUserAndReturnsWebStatistics()
    {
        var service = new QuizServiceFake
        {
            UserStatistics = new UserStatisticsReadModel
            {
                TotalAttempts = 3,
                PassedAttempts = 2,
                AveragePercentage = 67.24,
                BestPercentage = 90,
                RecentScores = [new() { CreatedAt = new DateTime(2026, 11, 9, 18, 0, 0, DateTimeKind.Utc), Percentage = 90 }],
                DeckStats = [new() { Name = "Bộ đề", ParentName = "Môn học", Attempts = 3 }]
            }
        };
        using var host = new ApiTestHost
        {
            ConfigureServices = services => services.AddSingleton<IQuizService>(service)
        };
        var user = host.AddUser();
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        const string path = "/api/history/statistics?userId=someone-else";
        using var unauthorized = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Null(service.StatisticsUserId);

        ApiTestHost.UseToken(client, await host.LoginAsync(client));
        var result = await client.GetFromJsonAsync<StatisticsViewModel>(path);

        Assert.NotNull(result);
        Assert.Equal(user.Id, service.StatisticsUserId);
        Assert.Equal(3, result.TotalAttempts);
        Assert.Equal(67.2, result.AveragePercentage);
        Assert.Equal(90, result.BestPercentage);
        Assert.Equal(66.7, result.PassRate);
        Assert.Equal("10/11", Assert.Single(result.RecentScores).Label);
        Assert.Equal("Môn học / Bộ đề", Assert.Single(result.DeckStats).Name);
    }

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
        public string? StatisticsUserId { get; private set; }

        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId)
        {
            StatisticsUserId = userId;
            return Task.FromResult(UserStatistics);
        }
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
