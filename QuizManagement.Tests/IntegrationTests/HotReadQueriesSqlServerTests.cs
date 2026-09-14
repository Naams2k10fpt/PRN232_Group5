using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class HotReadQueriesSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task HotReads_AggregateAndPageInSqlWithoutTrackingEntityGraphs()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            Seed(setup);
        }

        try
        {
            using var context = new QuizManagementDbContext(options);
            var quizRepository = new QuizRepository(context);

            var historyPage = await quizRepository.GetTestHistoryPageAsync("user-00", 1, 10);
            var userStatistics = await quizRepository.GetUserStatisticsAsync("user-00");
            var deckId = await context.Decks
                .Where(deck => deck.Subject.UserId == "mentor-a")
                .Select(deck => deck.Id)
                .SingleAsync();
            var leaderboard = await quizRepository.GetLeaderboardAsync(deckId, 20);
            var mentorStatistics = await quizRepository.GetMentorStatisticsAsync("mentor-a", false);

            Assert.Equal(60, historyPage.TotalCount);
            Assert.Equal(10, historyPage.Items.Count);
            Assert.Equal(60, userStatistics.TotalAttempts);
            Assert.Equal(2, userStatistics.SubjectStats.Count);
            Assert.Equal(2, userStatistics.DeckStats.Count);
            Assert.All(userStatistics.SubjectStats, group => Assert.Equal("Same subject", group.Name));
            Assert.All(userStatistics.DeckStats, group => Assert.Equal("Same deck", group.Name));
            Assert.Equal(12, userStatistics.RecentScores.Count);
            Assert.Equal(20, leaderboard.Count);
            Assert.Equal(25, mentorStatistics.UniqueUsers);
            Assert.Equal(54, mentorStatistics.TotalAttempts);
            Assert.Empty(context.ChangeTracker.Entries<TestHistory>());

            var reportRepository = new QuestionReportRepository(context);
            var reportPage = await reportRepository.GetPageAsync("mentor-a", false, 1, 50);
            Assert.Equal(60, reportPage.TotalCount);
            Assert.Equal(50, reportPage.Reports.Count);
            Assert.Empty(context.ChangeTracker.Entries<QuestionReport>());

            var loginRepository = new LoginAttemptRepository(context);
            var failedLogins = await loginRepository.GetRecentAsync(300, success: false);
            Assert.Equal(300, failedLogins.Count);
            Assert.All(failedLogins, attempt => Assert.False(attempt.IsSuccess));
            Assert.Empty(context.ChangeTracker.Entries<LoginAttempt>());
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }

    private static void Seed(QuizManagementDbContext context)
    {
        var mentors = new[]
        {
            CreateUser("mentor-a"),
            CreateUser("mentor-b")
        };
        var users = Enumerable.Range(0, 25)
            .Select(index => CreateUser($"user-{index:00}"))
            .ToList();
        context.Users.AddRange(mentors.Concat(users));

        var subjectA = new Subject { UserId = "mentor-a", Name = "Same subject" };
        var subjectB = new Subject { UserId = "mentor-b", Name = "Same subject" };
        var deckA = new Deck { Subject = subjectA, Name = "Same deck" };
        var deckB = new Deck { Subject = subjectB, Name = "Same deck" };
        context.Decks.AddRange(deckA, deckB);
        context.SaveChanges();

        var startedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.TestHistories.AddRange(Enumerable.Range(0, 60).Select(index => new TestHistory
        {
            UserId = "user-00",
            DeckId = index % 2 == 0 ? deckA.Id : deckB.Id,
            Score = index % 11,
            Percentage = index % 2 == 0 ? 80 : 60,
            CreatedAt = startedAt.AddMinutes(index)
        }));
        context.TestHistories.AddRange(users.Skip(1).Select((user, index) => new TestHistory
        {
            UserId = user.Id,
            DeckId = deckA.Id,
            Score = 5,
            Percentage = 50 + index,
            CreatedAt = startedAt.AddHours(2).AddMinutes(index)
        }));

        var questions = Enumerable.Range(0, 60).Select(index => new Question
        {
            DeckId = deckA.Id,
            Content = $"Question {index}",
            QuestionType = 1,
            CreatedAt = startedAt
        }).ToList();
        context.Questions.AddRange(questions);
        context.SaveChanges();
        context.QuestionReports.AddRange(questions.Select((question, index) => new QuestionReport
        {
            QuestionId = question.Id,
            UserId = "user-00",
            Reason = "WrongAnswer",
            IsResolved = index % 2 == 0,
            CreatedAt = startedAt.AddMinutes(index)
        }));

        context.LoginAttempts.AddRange(Enumerable.Range(0, 400).Select(index => new LoginAttempt
        {
            Email = $"attempt-{index}@test.local",
            IpAddress = "192.0.2.10",
            IsSuccess = index >= 350,
            CreatedAt = startedAt.AddSeconds(index)
        }));
        context.SaveChanges();
    }

    private static User CreateUser(string id) => new()
    {
        Id = id,
        Username = id,
        Email = $"{id}@test.local",
        Role = id.StartsWith("mentor", StringComparison.Ordinal) ? AppRoles.Mentor : AppRoles.User,
        SecurityStamp = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow
    };
}
