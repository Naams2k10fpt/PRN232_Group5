using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuestionImportAtomicSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void AddQuestions_WhenBatchIsValid_PersistsEveryQuestionAndAnswer()
    {
        WithDatabase((options, deckId) =>
        {
            using (var context = new QuizManagementDbContext(options))
            {
                var service = new QuestionService(new QuestionRepository(context));
                service.AddQuestions(
                [
                    CreateQuestion(deckId, "First"),
                    CreateQuestion(deckId, "Second")
                ]);
            }

            using var verification = new QuizManagementDbContext(options);
            Assert.Equal(2, verification.Questions.Count());
            Assert.Equal(4, verification.Answers.Count());
        });
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void AddQuestions_WhenLaterInsertFails_RollsBackTheWholeBatch()
    {
        WithDatabase((options, deckId) =>
        {
            using (var context = new QuizManagementDbContext(options))
            {
                var service = new QuestionService(new QuestionRepository(context));

                Assert.Throws<DbUpdateException>(() => service.AddQuestions(
                [
                    CreateQuestion(deckId, "Would otherwise succeed"),
                    CreateQuestion(int.MaxValue, "Invalid deck")
                ]));
            }

            using var verification = new QuizManagementDbContext(options);
            Assert.Empty(verification.Questions);
            Assert.Empty(verification.Answers);
        });
    }

    private static void WithDatabase(Action<DbContextOptions<QuizManagementDbContext>, int> test)
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            setup.Users.Add(new User
            {
                Id = "mentor",
                Username = "mentor",
                Email = "mentor@test.local",
                Role = AppRoles.Mentor,
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var subject = new Subject { UserId = "mentor", Name = "Subject" };
            setup.Subjects.Add(subject);
            setup.SaveChanges();
            var deck = new Deck { SubjectId = subject.Id, Name = "Deck" };
            setup.Decks.Add(deck);
            setup.SaveChanges();

            try
            {
                test(options, deck.Id);
            }
            finally
            {
                setup.Database.EnsureDeleted();
            }
        }
    }

    private static Question CreateQuestion(int deckId, string content) => new()
    {
        DeckId = deckId,
        Content = content,
        QuestionType = 1,
        Answers =
        [
            new Answer { Content = "Correct", IsCorrect = true },
            new Answer { Content = "Wrong" }
        ]
    };
}
