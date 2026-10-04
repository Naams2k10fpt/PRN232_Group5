using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.ApiModels;
using Services;
using Xunit;
using static QuizManagement.Tests.ApiTests.AuthApiTests;

namespace QuizManagement.Tests.ApiTests;

public class FlashcardsApiTests : IDisposable
{
    private static readonly DateTime NextReview = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    private readonly FlashcardServiceFake _flashcards = new();
    private readonly ApiTestHost _host;
    private string _userId = string.Empty;

    public FlashcardsApiTests()
    {
        _host = new ApiTestHost
        {
            ConfigureServices = services => services.AddSingleton<IFlashcardService>(_flashcards)
        };
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task BothEndpoints_WithoutToken_Return401Json()
    {
        var client = _host.CreateClient(new() { AllowAutoRedirect = false });

        await AssertError(await client.GetAsync("/api/decks/11/flashcards"), HttpStatusCode.Unauthorized, "unauthorized");
        await AssertError(
            await client.PostAsJsonAsync("/api/flashcards/progress", new { deckId = 11, questionId = 501, remembered = true }),
            HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(0, _flashcards.Calls);
    }

    [Theory]
    [InlineData("/api/decks/11/flashcards", false)]
    [InlineData("/api/decks/11/flashcards?mode=due", false)]
    [InlineData("/api/decks/11/flashcards?mode=all", true)]
    [InlineData("/api/decks/11/flashcards?mode=ALL", true)]
    public async Task List_MapsModeToDueOrAllCards_ForTheSignedInUser(string url, bool expectedIncludeAll)
    {
        _flashcards.Session = NewSession(expectedIncludeAll);
        var client = await SignedInClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((_userId, 11, expectedIncludeAll), _flashcards.LastSessionQuery);
    }

    [Fact]
    public async Task List_ReturnsTheDeckAndItsCards()
    {
        _flashcards.Session = NewSession(studyingAll: false);
        var client = await SignedInClient();

        var response = await client.GetAsync("/api/decks/11/flashcards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var deck = json.RootElement;
        Assert.Equal(11, deck.GetProperty("deckId").GetInt32());
        Assert.Equal("Chapter 1", deck.GetProperty("deckName").GetString());
        Assert.Equal("PRN232", deck.GetProperty("subjectName").GetString());
        Assert.Equal(30, deck.GetProperty("totalCards").GetInt32());
        Assert.Equal(2, deck.GetProperty("dueCards").GetInt32());
        Assert.False(deck.GetProperty("studyingAll").GetBoolean());
        Assert.Equal("2026-10-06T08:00:00Z", deck.GetProperty("nextReviewAtUtc").GetString());

        var cards = deck.GetProperty("cards").EnumerateArray().ToList();
        Assert.Equal(2, cards.Count);

        var newCard = cards[0];
        Assert.Equal(501, newCard.GetProperty("questionId").GetInt32());
        Assert.Equal("Front **501**", newCard.GetProperty("content").GetString());
        Assert.Equal(1, newCard.GetProperty("questionType").GetInt32());
        Assert.Equal("Why", newCard.GetProperty("explanation").GetString());
        Assert.Equal("Right", Assert.Single(newCard.GetProperty("correctAnswers").EnumerateArray()).GetString());
        var options = newCard.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal(9001, options[0].GetProperty("id").GetInt32());
        Assert.Equal("Right", options[0].GetProperty("content").GetString());
        Assert.True(options[0].GetProperty("isCorrect").GetBoolean());
        Assert.False(options[1].GetProperty("isCorrect").GetBoolean());
        Assert.Equal(JsonValueKind.Null, newCard.GetProperty("progress").ValueKind);

        var reviewedCard = cards[1];
        Assert.Equal(JsonValueKind.Null, reviewedCard.GetProperty("explanation").ValueKind);
        var progress = reviewedCard.GetProperty("progress");
        Assert.Equal(3, progress.GetProperty("repetition").GetInt32());
        Assert.Equal("2026-10-06T08:00:00Z", progress.GetProperty("nextReviewAtUtc").GetString());
    }

    [Fact]
    public async Task List_UnknownMode_Returns400OnMode_WithoutLoadingCards()
    {
        var client = await SignedInClient();

        var error = await AssertError(
            await client.GetAsync("/api/decks/11/flashcards?mode=later"), HttpStatusCode.BadRequest, "validation_failed");

        Assert.Contains("mode", error.Errors!.Keys);
        Assert.Equal(0, _flashcards.Calls);
    }

    [Fact]
    public async Task List_UnknownDeck_Returns404DeckNotFound()
    {
        _flashcards.Session = null;
        var client = await SignedInClient();

        await AssertError(await client.GetAsync("/api/decks/99/flashcards"), HttpStatusCode.NotFound, "deck_not_found");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Review_SavesTheAnswerForTheSignedInUser_AndReturnsTheNewSchedule(bool remembered)
    {
        _flashcards.ReviewResult = new FlashcardReviewResult(501, 2, 4320, NextReview);
        var client = await SignedInClient();

        var response = await client.PostAsJsonAsync(
            "/api/flashcards/progress", new { deckId = 11, questionId = 501, remembered });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((_userId, 11, 501, remembered), _flashcards.LastReview);
        Assert.Equal(
            new ReviewFlashcardResponse(501, 2, 4320, NextReview),
            await response.Content.ReadFromJsonAsync<ReviewFlashcardResponse>());
    }

    [Fact]
    public async Task Review_QuestionOutsideTheDeck_Returns404QuestionNotFound()
    {
        _flashcards.ReviewResult = null;
        var client = await SignedInClient();

        await AssertError(
            await client.PostAsJsonAsync("/api/flashcards/progress", new { deckId = 11, questionId = 999, remembered = true }),
            HttpStatusCode.NotFound, "question_not_found");
    }

    [Fact]
    public async Task Review_MissingRemembered_Returns400OnRemembered_WithoutSaving()
    {
        var client = await SignedInClient();

        var error = await AssertError(
            await client.PostAsJsonAsync("/api/flashcards/progress", new { deckId = 11, questionId = 501 }),
            HttpStatusCode.BadRequest, "validation_failed");

        Assert.Contains("remembered", error.Errors!.Keys);
        Assert.Equal(0, _flashcards.Calls);
    }

    [Fact]
    public async Task Review_NonPositiveIds_Return400OnBothIds_WithoutSaving()
    {
        var client = await SignedInClient();

        var error = await AssertError(
            await client.PostAsJsonAsync("/api/flashcards/progress", new { deckId = 0, questionId = -5, remembered = true }),
            HttpStatusCode.BadRequest, "validation_failed");

        Assert.Contains("deckId", error.Errors!.Keys);
        Assert.Contains("questionId", error.Errors!.Keys);
        Assert.Equal(0, _flashcards.Calls);
    }

    private async Task<HttpClient> SignedInClient()
    {
        _userId = _host.AddUser().Id;
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));
        return client;
    }

    private static FlashcardSession NewSession(bool studyingAll) => new(
        11,
        "Chapter 1",
        "PRN232",
        30,
        2,
        studyingAll,
        NextReview,
        [
            new FlashcardCard(
                501,
                "Front **501**",
                1,
                "Why",
                ["Right"],
                [new FlashcardOption(9001, "Right", true), new FlashcardOption(9002, "Wrong", false)],
                null,
                null),
            new FlashcardCard(
                502,
                "Front 502",
                2,
                null,
                ["Right"],
                [new FlashcardOption(9003, "Right", true)],
                3,
                NextReview)
        ]);

    private sealed class FlashcardServiceFake : IFlashcardService
    {
        public FlashcardSession? Session { get; set; }
        public FlashcardReviewResult? ReviewResult { get; set; }

        public int Calls { get; private set; }
        public (string UserId, int DeckId, bool IncludeAll)? LastSessionQuery { get; private set; }
        public (string UserId, int DeckId, int QuestionId, bool Remembered)? LastReview { get; private set; }

        public FlashcardSession? GetStudySession(string userId, int deckId, bool includeAll)
        {
            Calls++;
            LastSessionQuery = (userId, deckId, includeAll);
            return Session;
        }

        public FlashcardReviewResult? Review(string userId, int deckId, int questionId, bool remembered)
        {
            Calls++;
            LastReview = (userId, deckId, questionId, remembered);
            return ReviewResult;
        }
    }
}
