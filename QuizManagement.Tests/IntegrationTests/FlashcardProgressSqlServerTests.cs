using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class FlashcardProgressSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void ReviewFlashcard_PersistsScheduleAcrossContexts()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        int questionId;

        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            var question = new Question
            {
                Content = "Question",
                QuestionType = 1,
                Deck = new Deck
                {
                    Name = "Deck",
                    Subject = new Subject
                    {
                        Name = "Subject",
                        User = new User
                        {
                            Id = "user",
                            Username = "user",
                            Email = "user@test.local",
                            Role = AppRoles.User,
                            EmailConfirmed = true
                        }
                    }
                }
            };
            setup.Questions.Add(question);
            setup.SaveChanges();
            questionId = question.Id;
            new QuestionService(new QuestionRepository(setup))
                .ReviewFlashcard("user", questionId, true, now);
        }

        try
        {
            using var verification = new QuizManagementDbContext(options);
            var progress = Assert.Single(
                new QuestionService(new QuestionRepository(verification))
                    .GetFlashcardProgresses("user", verification.Decks.Single().Id));
            Assert.Equal(1, progress.Repetition);
            Assert.Equal(now.AddDays(1), progress.NextReviewAtUtc);
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }
}
