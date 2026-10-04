using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class CatalogReadQueriesPostgresTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void CatalogReads_CountInSqlSkipSoftDeletedRowsAndKeepProgressPerUser()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName))
            .Options;
        SeededIds ids;

        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            ids = Seed(setup);
        }

        try
        {
            using var context = new QuizManagementDbContext(options);
            var repository = new CatalogRepository(context);

            // Subjects: deleted subjects, decks and questions are neither listed nor counted.
            var allSubjects = repository.GetSubjectSummaries(null, 1, 10);
            Assert.Equal(2, allSubjects.TotalCount);
            Assert.Equal(2, allSubjects.Items.Count);
            var algebra = Assert.Single(allSubjects.Items, subject => subject.Id == ids.AlgebraSubjectId);
            Assert.Equal("Đại số", algebra.Name);
            Assert.Equal("mentor", algebra.OwnerName);
            Assert.Equal(1, algebra.DeckCount);
            Assert.Equal(3, algebra.QuestionCount);
            var empty = Assert.Single(allSubjects.Items, subject => subject.Id == ids.EmptySubjectId);
            Assert.Equal(1, empty.DeckCount);
            Assert.Equal(0, empty.QuestionCount);

            // Search ignores case, including Vietnamese letters, and surrounding spaces.
            Assert.Equal(ids.AlgebraSubjectId, Assert.Single(repository.GetSubjectSummaries(" đại ", 1, 10).Items).Id);
            Assert.Equal(ids.EmptySubjectId, Assert.Single(repository.GetSubjectSummaries("prn", 1, 10).Items).Id);
            Assert.Empty(repository.GetSubjectSummaries("deleted", 1, 10).Items);

            // Paging reports the full count and returns distinct pages.
            var firstPage = repository.GetSubjectSummaries(null, 1, 1);
            var secondPage = repository.GetSubjectSummaries(null, 2, 1);
            Assert.Equal(2, firstPage.TotalCount);
            Assert.NotEqual(Assert.Single(firstPage.Items).Id, Assert.Single(secondPage.Items).Id);

            Assert.Equal(algebra.QuestionCount, repository.GetSubjectSummary(ids.AlgebraSubjectId)!.QuestionCount);
            Assert.Null(repository.GetSubjectSummary(ids.DeletedSubjectId));
            Assert.True(repository.SubjectExists(ids.AlgebraSubjectId));
            Assert.False(repository.SubjectExists(ids.DeletedSubjectId));

            // Decks: learner-a remembered q1 (not due), forgot q2 (due again), never saw q3 (due).
            var deckForA = Assert.Single(repository.GetDeckSummaries(ids.AlgebraSubjectId, "learner-a", Now));
            Assert.Equal(ids.DeckId, deckForA.Id);
            Assert.Equal("Deck A", deckForA.Name);
            Assert.Equal("Đại số", deckForA.SubjectName);
            Assert.Equal(15, deckForA.TimeLimitMinutes);
            Assert.Equal(3, deckForA.QuestionCount);
            Assert.Equal(2, deckForA.DueCount);
            Assert.Equal(1, deckForA.LearnedCount);
            Assert.Equal(Now.AddHours(-1).AddDays(1), deckForA.NextReviewAtUtc);

            // learner-b has no progress: everything is due, nothing is learned.
            var deckForB = repository.GetDeckSummary(ids.DeckId, "learner-b", Now)!;
            Assert.Equal(3, deckForB.QuestionCount);
            Assert.Equal(3, deckForB.DueCount);
            Assert.Equal(0, deckForB.LearnedCount);
            Assert.Null(deckForB.NextReviewAtUtc);

            Assert.Null(repository.GetDeckSummary(ids.DeletedDeckId, "learner-a", Now));
            Assert.Empty(repository.GetDeckSummaries(ids.DeletedSubjectId, "learner-a", Now));

            // Flashcards: live questions of the deck with their answers.
            var flashcards = repository.GetFlashcardsByDeck(ids.DeckId);
            Assert.Equal(3, flashcards.Count);
            Assert.DoesNotContain(flashcards, question => question.Id == ids.DeletedQuestionId);
            Assert.All(flashcards, question => Assert.Equal(2, question.Answers.Count));

            Assert.True(repository.QuestionBelongsToDeck(ids.DeckId, ids.FirstQuestionId));
            Assert.False(repository.QuestionBelongsToDeck(ids.DeckId, ids.DeletedQuestionId));
            Assert.False(repository.QuestionBelongsToDeck(ids.EmptyDeckId, ids.FirstQuestionId));

            Assert.Empty(context.ChangeTracker.Entries());
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }

    private static SeededIds Seed(QuizManagementDbContext context)
    {
        context.Users.AddRange(CreateUser("mentor"), CreateUser("learner-a"), CreateUser("learner-b"));

        var questions = Enumerable.Range(1, 3).Select(number => CreateQuestion($"Question {number}")).ToList();
        var deletedQuestion = CreateQuestion("Deleted question");
        deletedQuestion.IsDeleted = true;

        var deck = new Deck { Name = "Deck A", TimeLimitMinutes = 15 };
        foreach (var question in questions.Append(deletedQuestion))
            deck.Questions.Add(question);

        var deletedDeckQuestion = CreateQuestion("Question in a deleted deck");
        deletedDeckQuestion.IsDeleted = true;
        var deletedDeck = new Deck { Name = "Deleted deck", IsDeleted = true };
        deletedDeck.Questions.Add(deletedDeckQuestion);

        var algebra = new Subject { UserId = "mentor", Name = "Đại số" };
        algebra.Decks.Add(deck);
        algebra.Decks.Add(deletedDeck);

        var emptyDeck = new Deck { Name = "Empty deck" };
        var emptySubject = new Subject { UserId = "mentor", Name = "PRN232" };
        emptySubject.Decks.Add(emptyDeck);

        var deletedSubject = new Subject { UserId = "mentor", Name = "Deleted subject", IsDeleted = true };

        context.Subjects.AddRange(algebra, emptySubject, deletedSubject);
        context.SaveChanges();

        var remembered = new FlashcardProgress { UserId = "learner-a", QuestionId = questions[0].Id };
        remembered.Review(true, Now.AddHours(-1));
        var forgotten = new FlashcardProgress { UserId = "learner-a", QuestionId = questions[1].Id };
        forgotten.Review(false, Now.AddHours(-1));
        context.FlashcardProgresses.AddRange(remembered, forgotten);
        context.SaveChanges();

        return new SeededIds(
            algebra.Id,
            emptySubject.Id,
            deletedSubject.Id,
            deck.Id,
            emptyDeck.Id,
            deletedDeck.Id,
            questions[0].Id,
            deletedQuestion.Id);
    }

    private static Question CreateQuestion(string content) => new()
    {
        Content = content,
        QuestionType = 1,
        Answers =
        [
            new Answer { Content = "Correct", IsCorrect = true },
            new Answer { Content = "Incorrect", IsCorrect = false }
        ]
    };

    private static User CreateUser(string id) => new()
    {
        Id = id,
        Username = id,
        Email = $"{id}@test.local",
        Role = AppRoles.User,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private sealed record SeededIds(
        int AlgebraSubjectId,
        int EmptySubjectId,
        int DeletedSubjectId,
        int DeckId,
        int EmptyDeckId,
        int DeletedDeckId,
        int FirstQuestionId,
        int DeletedQuestionId);
}
