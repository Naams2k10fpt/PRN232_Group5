using BusinessObjects;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.ServiceTests;

public class FlashcardServiceTests
{
    private const int DeckId = 7;
    private const string UserId = "user-a";
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    [Fact]
    public void GetStudySession_ReturnsNull_WhenDeckDoesNotExist()
    {
        var service = CreateService(new CatalogRepositoryFake(), new QuestionRepositoryFake(), deck: null);

        Assert.Null(service.GetStudySession(UserId, DeckId, includeAll: false));
    }

    [Fact]
    public void GetStudySession_ReturnsOnlyDueCards_NewCardsFirstThenLongestOverdue()
    {
        var catalog = new CatalogRepositoryFake
        {
            Questions = [NewQuestion(1), NewQuestion(2), NewQuestion(3), NewQuestion(4)]
        };
        var questions = new QuestionRepositoryFake
        {
            Progresses =
            [
                NewProgress(1, repetition: 1, nextReviewAtUtc: NowUtc.AddHours(-1)),
                NewProgress(2, repetition: 2, nextReviewAtUtc: NowUtc.AddDays(2)),
                NewProgress(3, repetition: 0, nextReviewAtUtc: NowUtc.AddDays(-3))
            ]
        };

        var session = CreateService(catalog, questions).GetStudySession(UserId, DeckId, includeAll: false)!;

        Assert.Equal(new[] { 4, 3, 1 }, session.Cards.Select(card => card.QuestionId).ToArray());
        Assert.Equal(4, session.TotalCards);
        Assert.Equal(3, session.DueCards);
        Assert.False(session.StudyingAll);
        Assert.Equal(NowUtc.AddDays(2), session.NextReviewAtUtc);
        Assert.Equal((DeckId, UserId), questions.LastProgressLookup);
    }

    [Fact]
    public void GetStudySession_IncludesCardsThatAreNotDue_WhenIncludeAll()
    {
        var catalog = new CatalogRepositoryFake { Questions = [NewQuestion(1), NewQuestion(2)] };
        var questions = new QuestionRepositoryFake
        {
            Progresses = [NewProgress(2, repetition: 2, nextReviewAtUtc: NowUtc.AddDays(2))]
        };

        var session = CreateService(catalog, questions).GetStudySession(UserId, DeckId, includeAll: true)!;

        Assert.Equal(new[] { 1, 2 }, session.Cards.Select(card => card.QuestionId).ToArray());
        Assert.Equal(2, session.TotalCards);
        Assert.Equal(1, session.DueCards);
        Assert.True(session.StudyingAll);
    }

    [Fact]
    public void GetStudySession_TreatsACardDueExactlyNowAsDue()
    {
        var catalog = new CatalogRepositoryFake { Questions = [NewQuestion(1)] };
        var questions = new QuestionRepositoryFake
        {
            Progresses = [NewProgress(1, repetition: 1, nextReviewAtUtc: NowUtc)]
        };

        var session = CreateService(catalog, questions).GetStudySession(UserId, DeckId, includeAll: false)!;

        Assert.Single(session.Cards);
        Assert.Null(session.NextReviewAtUtc);
    }

    [Fact]
    public void GetStudySession_MapsDeckAnswersAndProgressOntoTheCard()
    {
        var question = new Question
        {
            Id = 1,
            DeckId = DeckId,
            Content = "Front",
            Explanation = "Why",
            QuestionType = 2,
            Answers =
            [
                new Answer { Id = 12, Content = "C", IsCorrect = true },
                new Answer { Id = 10, Content = "A", IsCorrect = true },
                new Answer { Id = 11, Content = "B", IsCorrect = false }
            ]
        };
        var catalog = new CatalogRepositoryFake { Questions = [question, NewQuestion(2)] };
        var questions = new QuestionRepositoryFake
        {
            Progresses = [NewProgress(1, repetition: 3, nextReviewAtUtc: NowUtc.AddMinutes(-5))]
        };

        var session = CreateService(catalog, questions).GetStudySession(UserId, DeckId, includeAll: false)!;

        Assert.Equal(DeckId, session.DeckId);
        Assert.Equal("Deck", session.DeckName);
        Assert.Equal("Subject", session.SubjectName);

        var newCard = session.Cards[0];
        Assert.Equal(2, newCard.QuestionId);
        Assert.Null(newCard.Repetition);
        Assert.Null(newCard.NextReviewAtUtc);

        var reviewedCard = session.Cards[1];
        Assert.Equal(1, reviewedCard.QuestionId);
        Assert.Equal("Front", reviewedCard.Content);
        Assert.Equal("Why", reviewedCard.Explanation);
        Assert.Equal(2, reviewedCard.QuestionType);
        Assert.Equal(new[] { "A", "C" }, reviewedCard.CorrectAnswers.ToArray());
        Assert.Equal(
            new[]
            {
                new FlashcardOption(10, "A", true),
                new FlashcardOption(11, "B", false),
                new FlashcardOption(12, "C", true)
            },
            reviewedCard.Options.ToArray());
        Assert.Equal(3, reviewedCard.Repetition);
        Assert.Equal(NowUtc.AddMinutes(-5), reviewedCard.NextReviewAtUtc);
    }

    [Fact]
    public void Review_ReturnsNullWithoutSaving_WhenQuestionIsNotInTheDeck()
    {
        var catalog = new CatalogRepositoryFake { QuestionInDeck = false };
        var questions = new QuestionRepositoryFake();

        var result = CreateService(catalog, questions).Review(UserId, DeckId, 99, remembered: true);

        Assert.Null(result);
        Assert.Equal((DeckId, 99), catalog.LastMembershipLookup);
        Assert.Equal(0, questions.ReviewCalls);
    }

    [Fact]
    public void Review_SavesTheReviewAtTheCurrentTimeAndReturnsTheNewSchedule()
    {
        var catalog = new CatalogRepositoryFake { QuestionInDeck = true };
        var questions = new QuestionRepositoryFake();

        var result = CreateService(catalog, questions).Review(UserId, DeckId, 5, remembered: true)!;

        Assert.Equal((UserId, 5, true, NowUtc), questions.LastReview);
        Assert.Equal(5, result.QuestionId);
        Assert.Equal(1, result.Repetition);
        Assert.Equal(24 * 60, result.IntervalMinutes);
        Assert.Equal(NowUtc.AddDays(1), result.NextReviewAtUtc);
    }

    private static FlashcardService CreateService(
        CatalogRepositoryFake catalog,
        QuestionRepositoryFake questions)
        => CreateService(
            catalog,
            questions,
            new Deck { Id = DeckId, Name = "Deck", Subject = new Subject { Name = "Subject" } });

    private static FlashcardService CreateService(
        CatalogRepositoryFake catalog,
        QuestionRepositoryFake questions,
        Deck? deck)
        => new(new DeckRepositoryFake(deck), questions, catalog, new FixedTimeProvider(Now));

    private static Question NewQuestion(int id) => new()
    {
        Id = id,
        DeckId = DeckId,
        Content = $"Question {id}",
        QuestionType = 1,
        Answers =
        [
            new Answer { Id = id * 10, Content = "Correct", IsCorrect = true },
            new Answer { Id = id * 10 + 1, Content = "Incorrect", IsCorrect = false }
        ]
    };

    private static FlashcardProgress NewProgress(int questionId, int repetition, DateTime nextReviewAtUtc) => new()
    {
        UserId = UserId,
        QuestionId = questionId,
        Repetition = repetition,
        NextReviewAtUtc = nextReviewAtUtc
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DeckRepositoryFake(Deck? studyDeck) : IDeckRepository
    {
        public Deck? GetDeckForStudy(int id) => studyDeck;

        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotSupportedException();
        public void AddDeck(Deck deck) => throw new NotSupportedException();
        public void UpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }

    private sealed class CatalogRepositoryFake : ICatalogRepository
    {
        public List<Question> Questions { get; init; } = [];
        public bool QuestionInDeck { get; init; }
        public (int DeckId, int QuestionId)? LastMembershipLookup { get; private set; }

        public List<Question> GetFlashcardsByDeck(int deckId) => Questions;

        public bool QuestionBelongsToDeck(int deckId, int questionId)
        {
            LastMembershipLookup = (deckId, questionId);
            return QuestionInDeck;
        }

        public PagedResult<SubjectSummaryReadModel> GetSubjectSummaries(string? search, int page, int pageSize)
            => throw new NotSupportedException();
        public SubjectSummaryReadModel? GetSubjectSummary(int id) => throw new NotSupportedException();
        public bool SubjectExists(int id) => throw new NotSupportedException();
        public List<DeckSummaryReadModel> GetDeckSummaries(int subjectId, string userId, DateTime nowUtc)
            => throw new NotSupportedException();
        public DeckSummaryReadModel? GetDeckSummary(int deckId, string userId, DateTime nowUtc)
            => throw new NotSupportedException();
    }

    private sealed class QuestionRepositoryFake : IQuestionRepository
    {
        public List<FlashcardProgress> Progresses { get; init; } = [];
        public (int DeckId, string UserId)? LastProgressLookup { get; private set; }
        public (string UserId, int QuestionId, bool Remembered, DateTime ReviewedAtUtc)? LastReview { get; private set; }
        public int ReviewCalls { get; private set; }

        public IEnumerable<FlashcardProgress> GetFlashcardProgresses(string userId, int deckId)
        {
            LastProgressLookup = (deckId, userId);
            return Progresses;
        }

        public FlashcardProgress ReviewFlashcard(string userId, int questionId, bool remembered, DateTime reviewedAtUtc, Guid? reviewId = null)
        {
            ReviewCalls++;
            LastReview = (userId, questionId, remembered, reviewedAtUtc);
            var progress = new FlashcardProgress { UserId = userId, QuestionId = questionId };
            progress.Review(remembered, reviewedAtUtc);
            return progress;
        }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId) => throw new NotSupportedException();
        public Question? GetQuestionById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public void AddQuestion(Question question) => throw new NotSupportedException();
        public void AddQuestions(IEnumerable<Question> questions) => throw new NotSupportedException();
        public QuestionUpdateResult TryUpdateQuestion(Question question) => throw new NotSupportedException();
        public void DeleteQuestion(Question question) => throw new NotSupportedException();
    }
}
