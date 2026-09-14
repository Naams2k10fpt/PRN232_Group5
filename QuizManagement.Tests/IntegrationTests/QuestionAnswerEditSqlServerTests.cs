using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuestionAnswerEditSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void TryUpdateQuestion_RejectsReferencedRemovalAndAllowsUnreferencedRemoval()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var question = SeedQuestionWithHistory(context);
            var referencedId = question.Answers.Single(answer => answer.Content == "Referenced").Id;
            var keptId = question.Answers.Single(answer => answer.Content == "Kept").Id;
            var unreferencedId = question.Answers.Single(answer => answer.Content == "Unreferenced").Id;

            var rejected = QuestionDAO.Instance.TryUpdateQuestion(context, new Question
            {
                Id = question.Id,
                Content = "Should not be saved",
                QuestionType = 1,
                Answers =
                [
                    new Answer { Id = keptId, Content = "Kept", IsCorrect = true },
                    new Answer { Id = unreferencedId, Content = "Unreferenced" }
                ]
            });

            Assert.Equal(QuestionUpdateResult.ReferencedAnswer, rejected);
            context.ChangeTracker.Clear();
            var unchanged = context.Questions.Include(item => item.Answers).Single(item => item.Id == question.Id);
            Assert.Equal("Original", unchanged.Content);
            Assert.Equal(3, unchanged.Answers.Count);
            Assert.Equal(referencedId, context.TestResultDetails.Single().SelectedAnswerId);

            var updated = QuestionDAO.Instance.TryUpdateQuestion(context, new Question
            {
                Id = question.Id,
                RowVersion = question.RowVersion,
                Content = "Updated",
                QuestionType = 1,
                Answers =
                [
                    new Answer { Id = referencedId, Content = "Referenced", IsCorrect = true },
                    new Answer { Id = keptId, Content = "Kept" }
                ]
            });

            Assert.Equal(QuestionUpdateResult.Updated, updated);
            context.ChangeTracker.Clear();
            var persisted = context.Questions.Include(item => item.Answers).Single(item => item.Id == question.Id);
            Assert.Equal("Updated", persisted.Content);
            Assert.Contains(persisted.Answers, answer => answer.Id == referencedId);
            Assert.Contains(persisted.Answers, answer => answer.Id == keptId);
            Assert.DoesNotContain(persisted.Answers, answer => answer.Id == unreferencedId);
            Assert.Equal(referencedId, context.TestResultDetails.Single().SelectedAnswerId);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static Question SeedQuestionWithHistory(QuizManagementDbContext context)
    {
        var owner = CreateUser("mentor", AppRoles.Mentor);
        var reporter = CreateUser("reporter", AppRoles.User);
        var referenced = new Answer { Content = "Referenced", IsCorrect = true };
        var question = new Question
        {
            Content = "Original",
            QuestionType = 1,
            CreatedAt = DateTime.UtcNow,
            Answers =
            [
                referenced,
                new Answer { Content = "Kept" },
                new Answer { Content = "Unreferenced" }
            ],
            Deck = new Deck
            {
                Name = "Deck",
                CreatedAt = DateTime.UtcNow,
                Subject = new Subject
                {
                    Name = "Subject",
                    User = owner,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };
        context.TestHistories.Add(new TestHistory
        {
            User = reporter,
            Deck = question.Deck,
            Score = 10,
            Percentage = 100,
            CreatedAt = DateTime.UtcNow,
            TestResultDetails =
            [
                new TestResultDetail
                {
                    Question = question,
                    SelectedAnswer = referenced,
                    IsCorrect = true
                }
            ]
        });
        context.SaveChanges();
        return question;
    }

    private static User CreateUser(string id, string role) => new()
    {
        Id = id,
        Username = id,
        Email = $"{id}@test.local",
        Role = role,
        CreatedAt = DateTime.UtcNow
    };
}
