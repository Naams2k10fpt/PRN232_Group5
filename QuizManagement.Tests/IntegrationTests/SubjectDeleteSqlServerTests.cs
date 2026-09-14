using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class SubjectDeleteSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void DeleteSubject_SoftDeletesBufferedAggregate_WithoutChangingOtherSubject()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var owner = new User
            {
                Id = "mentor",
                Username = "mentor",
                Email = "mentor@test.local",
                Role = AppRoles.Mentor,
                CreatedAt = DateTime.UtcNow
            };
            var target = Subject(owner, "Target", deckCount: 2, questionsPerDeck: 2);
            var other = Subject(owner, "Other", deckCount: 1, questionsPerDeck: 1);
            context.Subjects.AddRange(target, other);
            context.SaveChanges();
            var targetId = target.Id;
            var targetDeckIds = target.Decks.Select(deck => deck.Id).ToList();
            var otherDeckId = other.Decks.Single().Id;
            context.ChangeTracker.Clear();

            var subject = context.Subjects.Single(item => item.Id == targetId);
            SubjectDAO.Instance.DeleteSubject(context, subject);
            context.ChangeTracker.Clear();

            Assert.True(context.Subjects.IgnoreQueryFilters().Single(item => item.Id == targetId).IsDeleted);
            Assert.All(
                context.Decks.IgnoreQueryFilters().Where(deck => targetDeckIds.Contains(deck.Id)),
                deck => Assert.True(deck.IsDeleted));
            Assert.All(
                context.Questions.IgnoreQueryFilters().Where(question => targetDeckIds.Contains(question.DeckId)),
                question => Assert.True(question.IsDeleted));
            Assert.False(context.Decks.IgnoreQueryFilters().Single(deck => deck.Id == otherDeckId).IsDeleted);
            Assert.False(context.Questions.IgnoreQueryFilters().Single(question => question.DeckId == otherDeckId).IsDeleted);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void DeleteSubject_HandlesEmptyAndPreviouslyDeletedChildren()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var oldTimestamp = DateTime.UtcNow.AddDays(-1);
            var owner = new User
            {
                Id = "mentor",
                Username = "mentor",
                Email = "mentor@test.local",
                Role = AppRoles.Mentor,
                CreatedAt = DateTime.UtcNow
            };
            var empty = Subject(owner, "Empty", deckCount: 0, questionsPerDeck: 0);
            var partial = Subject(owner, "Partial", deckCount: 1, questionsPerDeck: 2);
            var deletedDeck = partial.Decks.Single();
            deletedDeck.IsDeleted = true;
            deletedDeck.UpdatedAt = oldTimestamp;
            var deletedQuestion = deletedDeck.Questions.First();
            deletedQuestion.IsDeleted = true;
            deletedQuestion.UpdatedAt = oldTimestamp;
            context.Subjects.AddRange(empty, partial);
            context.SaveChanges();
            var emptyId = empty.Id;
            var partialId = partial.Id;
            var deletedDeckId = deletedDeck.Id;
            var deletedQuestionId = deletedQuestion.Id;
            context.ChangeTracker.Clear();

            SubjectDAO.Instance.DeleteSubject(context, context.Subjects.Single(subject => subject.Id == emptyId));
            context.ChangeTracker.Clear();
            SubjectDAO.Instance.DeleteSubject(context, context.Subjects.Single(subject => subject.Id == partialId));
            context.ChangeTracker.Clear();

            Assert.True(context.Subjects.IgnoreQueryFilters().Single(subject => subject.Id == emptyId).IsDeleted);
            Assert.True(context.Subjects.IgnoreQueryFilters().Single(subject => subject.Id == partialId).IsDeleted);
            var persistedDeck = context.Decks.IgnoreQueryFilters().Single(deck => deck.Id == deletedDeckId);
            Assert.True(persistedDeck.IsDeleted);
            Assert.Equal(oldTimestamp, persistedDeck.UpdatedAt);
            var persistedQuestions = context.Questions.IgnoreQueryFilters()
                .Where(question => question.DeckId == deletedDeckId)
                .ToList();
            Assert.All(persistedQuestions, question => Assert.True(question.IsDeleted));
            Assert.Equal(oldTimestamp, persistedQuestions.Single(question => question.Id == deletedQuestionId).UpdatedAt);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static Subject Subject(User owner, string name, int deckCount, int questionsPerDeck)
    {
        var subject = new Subject
        {
            Name = name,
            User = owner,
            CreatedAt = DateTime.UtcNow
        };

        for (var deckIndex = 0; deckIndex < deckCount; deckIndex++)
        {
            var deck = new Deck
            {
                Name = $"{name} Deck {deckIndex}",
                CreatedAt = DateTime.UtcNow
            };
            for (var questionIndex = 0; questionIndex < questionsPerDeck; questionIndex++)
            {
                deck.Questions.Add(new Question
                {
                    Content = $"Question {questionIndex}",
                    QuestionType = 1,
                    CreatedAt = DateTime.UtcNow
                });
            }
            subject.Decks.Add(deck);
        }

        return subject;
    }
}
