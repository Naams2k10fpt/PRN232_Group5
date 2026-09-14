using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuestionReportOwnershipSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void GetForResolution_ScopesMentorToOwnedContent_AndAllowsAdmin()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();

            var mentorA = User("mentor-a", AppRoles.Mentor);
            var mentorB = User("mentor-b", AppRoles.Mentor);
            var reporter = User("reporter", AppRoles.User);
            var reportA = ReportFor(mentorA, reporter, "Question A");
            var reportB = ReportFor(mentorB, reporter, "Question B");
            context.QuestionReports.AddRange(reportA, reportB);
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var own = QuestionReportDAO.Instance.GetForResolution(context, reportA.Id, mentorA.Id, allowAll: false);
            var foreign = QuestionReportDAO.Instance.GetForResolution(context, reportB.Id, mentorA.Id, allowAll: false);
            var admin = QuestionReportDAO.Instance.GetForResolution(context, reportB.Id, "admin", allowAll: true);

            Assert.NotNull(own);
            Assert.Null(foreign);
            Assert.NotNull(admin);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static User User(string id, string role) => new()
    {
        Id = id,
        Username = id,
        Email = $"{id}@test.local",
        Role = role,
        CreatedAt = DateTime.UtcNow
    };

    private static QuestionReport ReportFor(User owner, User reporter, string content) => new()
    {
        User = reporter,
        Reason = "WrongAnswer",
        CreatedAt = DateTime.UtcNow,
        Question = new Question
        {
            Content = content,
            QuestionType = 1,
            CreatedAt = DateTime.UtcNow,
            Deck = new Deck
            {
                Name = $"Deck {owner.Id}",
                CreatedAt = DateTime.UtcNow,
                Subject = new Subject
                {
                    Name = $"Subject {owner.Id}",
                    User = owner,
                    CreatedAt = DateTime.UtcNow
                }
            }
        }
    };
}
