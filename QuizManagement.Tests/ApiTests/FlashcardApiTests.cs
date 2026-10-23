using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.ApiModels;
using Services;
using Xunit;
using static QuizManagement.Tests.ApiTests.AuthApiTests;

namespace QuizManagement.Tests.ApiTests;

public class FlashcardApiTests : IDisposable
{
    private static readonly DateTime NextReview = new(2026, 10, 24, 8, 30, 0, DateTimeKind.Utc);

    private readonly FlashcardServiceFake _flashcards = new();
    private readonly ApiTestHost _host;

    public FlashcardApiTests()
    {
        _host = new ApiTestHost
        {
            ConfigureServices = services => services.AddSingleton<IFlashcardService>(_flashcards)
        };
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task StudyEndpoint_WithoutToken_Returns401Json()
    {
        var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        await AssertError(await client.GetAsync("/api/decks/1/flashcards"), HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(0, _flashcards.SessionCalls);
    }

    [Fact]
    public async Task ReviewEndpoint_WithoutToken_Returns401Json()
    {
        var client = _host.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsJsonAsync("/api/flashcards/progress", new { deckId = 1, questionId = 2, remembered = true });
        await AssertError(response, HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(0, _flashcards.ReviewCalls);
    }

    [Fact]
    public async Task GetSession_DueMode_PassesIncludeAllFalse_AndReturnsMappedSession()
    {
        _flashcards.Session = NewSession(1, false);
        var client = await SignedInClient();

        var response = await client.GetAsync("/api/decks/1/flashcards?mode=due");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((_userId, 1, false), _flashcards.LastSessionQuery);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("deckId").GetInt32());
        Assert.Equal("C# Basics", root.GetProperty("deckName").GetString());
        Assert.Equal("C# Fundamentals", root.GetProperty("subjectName").GetString());
        Assert.Equal(5, root.GetProperty("totalCards").GetInt32());
        Assert.Equal(2, root.GetProperty("dueCards").GetInt32());
        Assert.False(root.GetProperty("studyingAll").GetBoolean());

        var cards = root.GetProperty("cards");
        Assert.Equal(1, cards.GetArrayLength());
        var card = cards[0];
        Assert.Equal(10, card.GetProperty("questionId").GetInt32());
        Assert.Equal("What is CLR?", card.GetProperty("content").GetString());
        Assert.Equal(1, card.GetProperty("questionType").GetInt32());
        Assert.Equal("Common Language Runtime", card.GetProperty("explanation").GetString());

        var answers = card.GetProperty("correctAnswers");
        Assert.Equal(1, answers.GetArrayLength());
        Assert.Equal("Common Language Runtime", answers[0].GetString());

        var options = card.GetProperty("options");
        Assert.Equal(1, options.GetArrayLength());
        Assert.Equal(101, options[0].GetProperty("id").GetInt32());
        Assert.True(options[0].GetProperty("isCorrect").GetBoolean());

        var progress = card.GetProperty("progress");
        Assert.Equal(2, progress.GetProperty("repetition").GetInt32());
        Assert.Equal("2026-10-24T08:30:00Z", progress.GetProperty("nextReviewAtUtc").GetString());
    }

    [Fact]
    public async Task GetSession_AllMode_PassesIncludeAllTrue()
    {
        _flashcards.Session = NewSession(1, true);
        var client = await SignedInClient();

        var response = await client.GetAsync("/api/decks/1/flashcards?mode=all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((_userId, 1, true), _flashcards.LastSessionQuery);
    }

    [Fact]
    public async Task GetSession_NotFound_Returns404DeckNotFound()
    {
        _flashcards.Session = null;
        var client = await SignedInClient();

        await AssertError(await client.GetAsync("/api/decks/999/flashcards"), HttpStatusCode.NotFound, "deck_not_found");
    }

    [Fact]
    public async Task Review_ReturnsUpdatedProgress()
    {
        _flashcards.ReviewResult = new FlashcardReviewResult(10, 3, 1440, NextReview);
        var client = await SignedInClient();

        var response = await client.PostAsJsonAsync("/api/flashcards/progress", new
        {
            deckId = 1,
            questionId = 10,
            remembered = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((_userId, 1, 10, true), _flashcards.LastReview);

        var result = await response.Content.ReadFromJsonAsync<ApiFlashcardReviewResponse>();
        Assert.NotNull(result);
        Assert.Equal(10, result.QuestionId);
        Assert.Equal(3, result.Repetition);
        Assert.Equal(1440, result.IntervalMinutes);
        Assert.Equal(NextReview, result.NextReviewAtUtc);
    }

    [Fact]
    public async Task Review_NotFound_Returns404QuestionNotFound()
    {
        _flashcards.ReviewResult = null;
        var client = await SignedInClient();

        var response = await client.PostAsJsonAsync("/api/flashcards/progress", new
        {
            deckId = 1,
            questionId = 999,
            remembered = false
        });

        await AssertError(response, HttpStatusCode.NotFound, "question_not_found");
    }

    private string _userId = string.Empty;

    private async Task<HttpClient> SignedInClient()
    {
        _userId = _host.AddUser().Id;
        var client = _host.CreateClient();
        ApiTestHost.UseToken(client, await _host.LoginAsync(client));
        return client;
    }

    private static FlashcardSession NewSession(int deckId, bool studyingAll) => new(
        deckId,
        "C# Basics",
        "C# Fundamentals",
        5,
        2,
        studyingAll,
        NextReview,
        new List<FlashcardCard>
        {
            new(
                10,
                "What is CLR?",
                1,
                "Common Language Runtime",
                new[] { "Common Language Runtime" },
                new[] { new FlashcardOption(101, "Common Language Runtime", true) },
                2,
                NextReview)
        });

    private sealed class FlashcardServiceFake : IFlashcardService
    {
        public FlashcardSession? Session { get; set; }
        public FlashcardReviewResult? ReviewResult { get; set; }

        public int SessionCalls { get; private set; }
        public int ReviewCalls { get; private set; }
        public (string UserId, int DeckId, bool IncludeAll)? LastSessionQuery { get; private set; }
        public (string UserId, int DeckId, int QuestionId, bool Remembered)? LastReview { get; private set; }

        public FlashcardSession? GetStudySession(string userId, int deckId, bool includeAll)
        {
            SessionCalls++;
            LastSessionQuery = (userId, deckId, includeAll);
            return Session;
        }

        public FlashcardReviewResult? Review(string userId, int deckId, int questionId, bool remembered)
        {
            ReviewCalls++;
            LastReview = (userId, deckId, questionId, remembered);
            return ReviewResult;
        }
    }
}
