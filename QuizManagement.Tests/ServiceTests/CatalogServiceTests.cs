using BusinessObjects;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.ServiceTests;

public class CatalogServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0, 1, CatalogService.DefaultPageSize)]
    [InlineData(-3, -1, 1, CatalogService.DefaultPageSize)]
    [InlineData(2, 50, 2, 50)]
    [InlineData(1, 1000, 1, CatalogService.MaxPageSize)]
    public void GetSubjects_ClampsPaging(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var repository = new CatalogRepositoryFake();

        CreateService(repository).GetSubjects(null, page, pageSize);

        Assert.Null(repository.LastSubjectQuery?.Search);
        Assert.Equal(expectedPage, repository.LastSubjectQuery?.Page);
        Assert.Equal(expectedPageSize, repository.LastSubjectQuery?.PageSize);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  prn  ", "prn")]
    public void GetSubjects_TrimsSearchAndTreatsBlankAsNoSearch(string? search, string? expected)
    {
        var repository = new CatalogRepositoryFake();

        CreateService(repository).GetSubjects(search, 1, 20);

        Assert.Equal(expected, repository.LastSubjectQuery?.Search);
    }

    [Fact]
    public void GetSubjects_CutsAnOverlongSearch()
    {
        var repository = new CatalogRepositoryFake();

        CreateService(repository).GetSubjects(new string('a', CatalogService.MaxSearchLength + 50), 1, 20);

        Assert.Equal(CatalogService.MaxSearchLength, repository.LastSubjectQuery?.Search?.Length);
    }

    [Fact]
    public void GetDecks_ReturnsNullWithoutQueryingDecks_WhenSubjectDoesNotExist()
    {
        var repository = new CatalogRepositoryFake { SubjectFound = false };

        Assert.Null(CreateService(repository).GetDecks(3, "user-a"));
        Assert.Null(repository.LastDeckListQuery);
    }

    [Fact]
    public void GetDecks_AsksForTheUsersProgressAtTheCurrentTime()
    {
        var repository = new CatalogRepositoryFake { SubjectFound = true };

        var decks = CreateService(repository).GetDecks(3, "user-a");

        Assert.NotNull(decks);
        Assert.Equal((3, "user-a", Now.UtcDateTime), repository.LastDeckListQuery);
    }

    [Fact]
    public void GetDeck_AsksForTheUsersProgressAtTheCurrentTime()
    {
        var repository = new CatalogRepositoryFake();

        CreateService(repository).GetDeck(11, "user-a");

        Assert.Equal((11, "user-a", Now.UtcDateTime), repository.LastDeckQuery);
    }

    private static CatalogService CreateService(CatalogRepositoryFake repository)
        => new(repository, new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CatalogRepositoryFake : ICatalogRepository
    {
        public bool SubjectFound { get; init; }
        public (string? Search, int Page, int PageSize)? LastSubjectQuery { get; private set; }
        public (int SubjectId, string UserId, DateTime NowUtc)? LastDeckListQuery { get; private set; }
        public (int DeckId, string UserId, DateTime NowUtc)? LastDeckQuery { get; private set; }

        public PagedResult<SubjectSummaryReadModel> GetSubjectSummaries(string? search, int page, int pageSize)
        {
            LastSubjectQuery = (search, page, pageSize);
            return new PagedResult<SubjectSummaryReadModel> { Page = page, PageSize = pageSize };
        }

        public bool SubjectExists(int id) => SubjectFound;

        public List<DeckSummaryReadModel> GetDeckSummaries(int subjectId, string userId, DateTime nowUtc)
        {
            LastDeckListQuery = (subjectId, userId, nowUtc);
            return [];
        }

        public DeckSummaryReadModel? GetDeckSummary(int deckId, string userId, DateTime nowUtc)
        {
            LastDeckQuery = (deckId, userId, nowUtc);
            return null;
        }

        public SubjectSummaryReadModel? GetSubjectSummary(int id) => throw new NotSupportedException();
        public List<Question> GetFlashcardsByDeck(int deckId) => throw new NotSupportedException();
        public bool QuestionBelongsToDeck(int deckId, int questionId) => throw new NotSupportedException();
    }
}
