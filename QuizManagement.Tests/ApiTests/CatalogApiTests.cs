using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessObjects;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.ApiModels;
using Services;
using Xunit;
using static QuizManagement.Tests.ApiTests.AuthApiTests;

namespace QuizManagement.Tests.ApiTests;

public class CatalogApiTests : IDisposable
{
    private static readonly DateTime CreatedAt = new(2026, 9, 20, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextReview = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    private readonly CatalogServiceFake _catalog = new();
    private readonly ApiTestHost _host;

    public CatalogApiTests()
    {
        _host = new ApiTestHost
        {
            ConfigureServices = services => services.AddSingleton<ICatalogService>(_catalog)
        };
    }

    public void Dispose() => _host.Dispose();

    [Theory]
    [InlineData("/api/subjects")]
    [InlineData("/api/subjects/3")]
    [InlineData("/api/subjects/3/decks")]
    [InlineData("/api/decks/11")]
    public async Task EveryEndpoint_WithoutToken_Returns401Json(string url)
    {
        var client = _host.CreateClient(new() { AllowAutoRedirect = false });

        await AssertError(await client.GetAsync(url), HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(0, _catalog.Calls);
    }

    [Fact]
    public async Task ListSubjects_ReturnsThePageWithCamelCaseCounts()
    {
        _catalog.Subjects = new PagedResult<SubjectSummaryReadModel>
        {
            Items = [NewSubject(3, "PRN232")],
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };
        var client = await SignedInClient();

        var response = await client.GetAsync("/api/subjects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(3, item.GetProperty("id").GetInt32());
        Assert.Equal("PRN232", item.GetProperty("name").GetString());
        Assert.Equal("mentor_demo", item.GetProperty("ownerName").GetString());
        Assert.Equal(4, item.GetProperty("deckCount").GetInt32());
        Assert.Equal(120, item.GetProperty("questionCount").GetInt32());
        Assert.Equal("2026-09-20T03:00:00Z", item.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task ListSubjects_UsesDefaultPaging_WhenNoQueryIsGiven()
    {
        var client = await SignedInClient();

        await client.GetAsync("/api/subjects");

        Assert.NotNull(_catalog.LastSubjectQuery);
        Assert.Null(_catalog.LastSubjectQuery?.Search);
        Assert.Equal(1, _catalog.LastSubjectQuery?.Page);
        Assert.Equal(CatalogService.DefaultPageSize, _catalog.LastSubjectQuery?.PageSize);
    }

    [Fact]
    public async Task ListSubjects_PassesSearchAndPaging_AndReportsWhatTheServiceUsed()
    {
        // The service clamps the page size; the response must report the clamped value, not the request.
        _catalog.Subjects = new PagedResult<SubjectSummaryReadModel> { Page = 2, PageSize = 100, TotalCount = 0 };
        var client = await SignedInClient();

        var page = await client.GetFromJsonAsync<ApiPage<ApiSubject>>("/api/subjects?search=prn&page=2&pageSize=1000");

        Assert.Equal(("prn", 2, 1000), _catalog.LastSubjectQuery);
        Assert.Equal(2, page!.Page);
        Assert.Equal(100, page.PageSize);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task ListSubjects_NonNumericPage_Returns400ValidationFailed()
    {
        var client = await SignedInClient();

        var error = await AssertError(
            await client.GetAsync("/api/subjects?page=abc"), HttpStatusCode.BadRequest, "validation_failed");

        Assert.Contains("page", error.Errors!.Keys);
        Assert.Equal(0, _catalog.Calls);
    }

    [Fact]
    public async Task GetSubject_ReturnsTheSubject()
    {
        _catalog.Subject = NewSubject(3, "PRN232");
        var client = await SignedInClient();

        var subject = await client.GetFromJsonAsync<ApiSubject>("/api/subjects/3");

        Assert.Equal(3, _catalog.LastSubjectId);
        Assert.Equal(new ApiSubject(3, "PRN232", "mentor_demo", 4, 120, CreatedAt), subject);
    }

    [Fact]
    public async Task GetSubject_Unknown_Returns404SubjectNotFound()
    {
        var client = await SignedInClient();

        await AssertError(await client.GetAsync("/api/subjects/99"), HttpStatusCode.NotFound, "subject_not_found");
    }

    [Fact]
    public async Task ListDecks_ReturnsDecksWithTheSignedInUsersProgress()
    {
        _catalog.Decks = [NewDeck(11, NextReview), NewDeck(12, null)];
        var client = await SignedInClient();

        var decks = await client.GetFromJsonAsync<List<ApiDeck>>("/api/subjects/3/decks");

        Assert.Equal((3, _userId), _catalog.LastDeckListQuery);
        Assert.Equal(
            new[]
            {
                new ApiDeck(11, 3, "PRN232", "Deck 11", 15, 30, 12, 18, NextReview),
                new ApiDeck(12, 3, "PRN232", "Deck 12", 15, 30, 12, 18, null)
            },
            decks!.ToArray());
    }

    [Fact]
    public async Task ListDecks_UnknownSubject_Returns404SubjectNotFound()
    {
        _catalog.Decks = null;
        var client = await SignedInClient();

        await AssertError(await client.GetAsync("/api/subjects/99/decks"), HttpStatusCode.NotFound, "subject_not_found");
    }

    [Fact]
    public async Task GetDeck_ReturnsTheDeckWithTheSignedInUsersProgress()
    {
        _catalog.Deck = NewDeck(11, NextReview);
        var client = await SignedInClient();

        var response = await client.GetAsync("/api/decks/11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((11, _userId), _catalog.LastDeckQuery);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var deck = json.RootElement;
        Assert.Equal(11, deck.GetProperty("id").GetInt32());
        Assert.Equal(3, deck.GetProperty("subjectId").GetInt32());
        Assert.Equal("PRN232", deck.GetProperty("subjectName").GetString());
        Assert.Equal("Deck 11", deck.GetProperty("name").GetString());
        Assert.Equal(15, deck.GetProperty("timeLimitMinutes").GetInt32());
        Assert.Equal(30, deck.GetProperty("questionCount").GetInt32());
        Assert.Equal(12, deck.GetProperty("dueCount").GetInt32());
        Assert.Equal(18, deck.GetProperty("learnedCount").GetInt32());
        Assert.Equal("2026-10-06T08:00:00Z", deck.GetProperty("nextReviewAtUtc").GetString());
    }

    [Fact]
    public async Task GetDeck_Unknown_Returns404DeckNotFound()
    {
        var client = await SignedInClient();

        await AssertError(await client.GetAsync("/api/decks/99"), HttpStatusCode.NotFound, "deck_not_found");
    }

    private string _userId = string.Empty;

    private async Task<HttpClient> SignedInClient()
    {
        _userId = _host.AddUser().Id;
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));
        return client;
    }

    private static SubjectSummaryReadModel NewSubject(int id, string name) => new()
    {
        Id = id,
        Name = name,
        OwnerName = "mentor_demo",
        DeckCount = 4,
        QuestionCount = 120,
        CreatedAt = CreatedAt
    };

    private static DeckSummaryReadModel NewDeck(int id, DateTime? nextReviewAtUtc) => new()
    {
        Id = id,
        SubjectId = 3,
        SubjectName = "PRN232",
        Name = $"Deck {id}",
        TimeLimitMinutes = 15,
        QuestionCount = 30,
        DueCount = 12,
        LearnedCount = 18,
        NextReviewAtUtc = nextReviewAtUtc
    };

    private sealed class CatalogServiceFake : ICatalogService
    {
        public PagedResult<SubjectSummaryReadModel> Subjects { get; set; } = new() { Page = 1, PageSize = 20 };
        public SubjectSummaryReadModel? Subject { get; set; }
        public IReadOnlyList<DeckSummaryReadModel>? Decks { get; set; } = [];
        public DeckSummaryReadModel? Deck { get; set; }

        public int Calls { get; private set; }
        public (string? Search, int Page, int PageSize)? LastSubjectQuery { get; private set; }
        public int? LastSubjectId { get; private set; }
        public (int SubjectId, string UserId)? LastDeckListQuery { get; private set; }
        public (int DeckId, string UserId)? LastDeckQuery { get; private set; }

        public PagedResult<SubjectSummaryReadModel> GetSubjects(string? search, int page, int pageSize)
        {
            Calls++;
            LastSubjectQuery = (search, page, pageSize);
            return Subjects;
        }

        public SubjectSummaryReadModel? GetSubject(int id)
        {
            Calls++;
            LastSubjectId = id;
            return Subject;
        }

        public IReadOnlyList<DeckSummaryReadModel>? GetDecks(int subjectId, string userId)
        {
            Calls++;
            LastDeckListQuery = (subjectId, userId);
            return Decks;
        }

        public DeckSummaryReadModel? GetDeck(int deckId, string userId)
        {
            Calls++;
            LastDeckQuery = (deckId, userId);
            return Deck;
        }
    }
}
