using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuestionConcurrencyPostgresTests
{
    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void StaleQuestionEdit_CannotOverwriteTheWinningEdit()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName))
            .Options;
        int questionId;
        int[] answerIds;
        uint staleVersion;

        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            var question = new Question
            {
                Content = "Original",
                QuestionType = 1,
                Answers =
                [
                    new Answer { Content = "A", IsCorrect = true },
                    new Answer { Content = "B" }
                ],
                Deck = new Deck
                {
                    Name = "Deck",
                    Subject = new Subject
                    {
                        Name = "Subject",
                        User = new User
                        {
                            Id = "mentor",
                            Username = "mentor",
                            Email = "mentor@test.local",
                            Role = AppRoles.Mentor
                        }
                    }
                }
            };
            setup.Questions.Add(question);
            setup.SaveChanges();
            questionId = question.Id;
            answerIds = question.Answers.OrderBy(answer => answer.Id).Select(answer => answer.Id).ToArray();
            staleVersion = question.RowVersion;
        }

        try
        {
            QuestionUpdateResult Update(string content, uint rowVersion)
            {
                using var context = new QuizManagementDbContext(options);
                return QuestionDAO.Instance.TryUpdateQuestion(context, new Question
                {
                    Id = questionId,
                    Content = content,
                    QuestionType = 1,
                    RowVersion = rowVersion,
                    Answers =
                    [
                        new Answer { Id = answerIds[0], Content = "A", IsCorrect = true },
                        new Answer { Id = answerIds[1], Content = "B" }
                    ]
                });
            }

            Assert.Equal(QuestionUpdateResult.Updated, Update("Winner", staleVersion));
            Assert.Equal(QuestionUpdateResult.ConcurrencyConflict, Update("Loser", staleVersion));

            using var verification = new QuizManagementDbContext(options);
            Assert.Equal("Winner", verification.Questions.Single(question => question.Id == questionId).Content);
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }
}
