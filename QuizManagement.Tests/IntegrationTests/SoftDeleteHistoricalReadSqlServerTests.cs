using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class SoftDeleteHistoricalReadSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void HistoryAndReports_RemainVisibleToAuthorizedActorsAfterAggregateSoftDelete()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var ownerA = CreateUser("mentor-a", AppRoles.Mentor);
            var ownerB = CreateUser("mentor-b", AppRoles.Mentor);
            var reporterA = CreateUser("reporter-a", AppRoles.User);
            var reporterB = CreateUser("reporter-b", AppRoles.User);
            var aggregateA = CreateAggregate(ownerA, reporterA, "A");
            var aggregateB = CreateAggregate(ownerB, reporterB, "B");
            context.AddRange(aggregateA.History, aggregateA.Report, aggregateB.History, aggregateB.Report);
            context.SaveChanges();

            aggregateA.Subject.IsDeleted = true;
            aggregateA.Deck.IsDeleted = true;
            aggregateA.Question.IsDeleted = true;
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var historyDao = QuizDAO.Instance;
            var detail = historyDao.GetTestHistoryById(context, aggregateA.History.Id, reporterA.Id);
            Assert.NotNull(detail);
            Assert.True(detail.Deck.IsDeleted);
            Assert.True(detail.Deck.Subject.IsDeleted);
            Assert.True(detail.TestResultDetails.Single().Question.IsDeleted);
            Assert.Null(historyDao.GetTestHistoryById(context, aggregateA.History.Id, reporterB.Id));
            Assert.Contains(
                historyDao.GetTestHistoriesByUser(context, reporterA.Id),
                history => history.Id == aggregateA.History.Id);
            Assert.Contains(
                historyDao.GetRecentTestHistories(context, reporterA.Id, 10),
                history => history.Id == aggregateA.History.Id);
            Assert.Contains(
                historyDao.GetTestHistoriesByDeck(context, aggregateA.Deck.Id),
                history => history.Id == aggregateA.History.Id);
            Assert.Contains(
                historyDao.GetTestHistoriesByContentOwner(context, ownerA.Id, isAdmin: false),
                history => history.Id == aggregateA.History.Id);
            Assert.DoesNotContain(
                historyDao.GetTestHistoriesByContentOwner(context, ownerB.Id, isAdmin: false),
                history => history.Id == aggregateA.History.Id);
            Assert.Contains(
                historyDao.GetTestHistoriesByContentOwner(context, "admin", isAdmin: true),
                history => history.Id == aggregateA.History.Id);

            var reportDao = QuestionReportDAO.Instance;
            var adminReports = reportDao.GetAll(context);
            Assert.Contains(adminReports, report => report.Id == aggregateA.Report.Id);
            var ownerReports = reportDao.GetByContentOwner(context, ownerA.Id);
            var deletedReport = Assert.Single(ownerReports, report => report.Id == aggregateA.Report.Id);
            Assert.True(deletedReport.Question.IsDeleted);
            Assert.True(deletedReport.Question.Deck.IsDeleted);
            Assert.True(deletedReport.Question.Deck.Subject.IsDeleted);
            Assert.DoesNotContain(
                reportDao.GetByContentOwner(context, ownerB.Id),
                report => report.Id == aggregateA.Report.Id);
            Assert.NotNull(reportDao.GetForResolution(
                context,
                aggregateA.Report.Id,
                ownerA.Id,
                allowAll: false));
            Assert.Null(reportDao.GetForResolution(
                context,
                aggregateA.Report.Id,
                ownerB.Id,
                allowAll: false));
            Assert.NotNull(reportDao.GetForResolution(
                context,
                aggregateA.Report.Id,
                "admin",
                allowAll: true));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static HistoricalAggregate CreateAggregate(User owner, User reporter, string suffix)
    {
        var subject = new Subject
        {
            Name = $"Subject {suffix}",
            User = owner,
            CreatedAt = DateTime.UtcNow
        };
        var deck = new Deck
        {
            Name = $"Deck {suffix}",
            Subject = subject,
            CreatedAt = DateTime.UtcNow
        };
        var correctAnswer = new Answer { Content = "Correct", IsCorrect = true };
        var question = new Question
        {
            Content = $"Question {suffix}",
            QuestionType = 1,
            Deck = deck,
            CreatedAt = DateTime.UtcNow,
            Answers = [correctAnswer]
        };
        var history = new TestHistory
        {
            User = reporter,
            Deck = deck,
            Score = 10,
            Percentage = 100,
            CreatedAt = DateTime.UtcNow,
            TestResultDetails =
            [
                new TestResultDetail
                {
                    Question = question,
                    SelectedAnswer = correctAnswer,
                    IsCorrect = true
                }
            ]
        };
        var report = new QuestionReport
        {
            User = reporter,
            Question = question,
            Reason = "WrongAnswer",
            CreatedAt = DateTime.UtcNow
        };
        return new HistoricalAggregate(subject, deck, question, history, report);
    }

    private static User CreateUser(string id, string role)
    {
        return new User
        {
            Id = id,
            Username = id,
            Email = $"{id}@test.local",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed record HistoricalAggregate(
        Subject Subject,
        Deck Deck,
        Question Question,
        TestHistory History,
        QuestionReport Report);
}
