using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizManagement.Tests.ApiTests;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public sealed class WebStudyTests
{
    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task CookieStudySharesFavoritesAndMissedProgressWithTheApi_AndRetriesAreIdempotent()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
        await using var db = new QuizManagementDbContext(options);
        using var host = new ApiTestHost
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<QuizManagementDbContext>();
                services.AddScoped(_ => new QuizManagementDbContext(options));
            }
        };
        try
        {
            await db.Database.MigrateAsync();
            var student = host.AddUser();
            var subject = new Subject { UserId = student.Id, Name = "Web study" };
            var deck = new Deck { Name = "Shared deck", Subject = subject };
            var missed = new Question { Content = "Missed", QuestionType = 1, Deck = deck };
            var unseen = new Question { Content = "Unseen", QuestionType = 1, Deck = deck };
            db.AddRange(student, missed, unseen);
            await db.SaveChangesAsync();

            using var web = host.CreateClient(new() { AllowAutoRedirect = false });
            var loginHtml = await web.GetStringAsync("/Account/Login");
            var login = await web.PostAsync("/Account/Login", Form(loginHtml,
                ("Email", student.Email), ("Password", ApiTestHost.Password)));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

            var subjectsHtml = await web.GetStringAsync("/Subjects");
            Assert.Contains("Yêu thích: Web study", WebUtility.HtmlDecode(subjectsHtml));
            var star = await web.PostAsync("/CatalogFavorites/Set", Form(subjectsHtml,
                ("kind", "subject"), ("id", subject.Id.ToString()), ("favorite", "true"),
                ("returnUrl", "https://external.invalid")));
            Assert.Equal(HttpStatusCode.Redirect, star.StatusCode);
            Assert.Equal("/Subjects", star.Headers.Location!.OriginalString);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await web.PostAsync("/CatalogFavorites/Set", new FormUrlEncodedContent(
                    new Dictionary<string, string> { ["kind"] = "subject", ["id"] = subject.Id.ToString(), ["favorite"] = "false" }))).StatusCode);

            using var api = host.CreateClient();
            ApiTestHost.UseToken(api, await host.LoginAsync(api));
            Assert.Equal([subject.Id], (await api.GetFromJsonAsync<int[]>("/api/favorites/subjects"))!);
            var decksHtml = await web.GetStringAsync($"/Decks?subjectId={subject.Id}");
            var deckStar = await web.PostAsync("/CatalogFavorites/Set", Form(decksHtml,
                ("kind", "deck"), ("id", deck.Id.ToString()), ("favorite", "true")));
            Assert.Equal(HttpStatusCode.Redirect, deckStar.StatusCode);
            Assert.Equal([deck.Id], (await api.GetFromJsonAsync<int[]>("/api/favorites/decks"))!);

            var study = await web.GetStringAsync($"/Flashcards/Study?deckId={deck.Id}");
            var missedReview = await web.PostAsync("/Flashcards/Review", Form(study,
                ("deckId", deck.Id.ToString()), ("questionId", missed.Id.ToString()),
                ("remembered", "false"), ("reviewId", Guid.NewGuid().ToString())));
            Assert.Equal(HttpStatusCode.OK, missedReview.StatusCode);
            var missedHtml = await web.GetStringAsync($"/Flashcards/Study?deckId={deck.Id}&missed=true");
            using var config = JsonDocument.Parse(Regex.Match(missedHtml,
                "<script id=\"flashcardConfig\" type=\"application/json\">(.*?)</script>",
                RegexOptions.Singleline).Groups[1].Value);
            var card = Assert.Single(config.RootElement.GetProperty("cards").EnumerateArray());
            Assert.Equal(missed.Id, card.GetProperty("QuestionId").GetInt32());

            var reviewId = Guid.NewGuid().ToString();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var saved = await web.PostAsync("/Flashcards/Review", Form(missedHtml,
                    ("deckId", deck.Id.ToString()), ("questionId", missed.Id.ToString()),
                    ("remembered", "true"), ("reviewId", reviewId)));
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            }
            Assert.Equal(1, (await db.FlashcardProgresses.AsNoTracking().SingleAsync()).Repetition);
            var emptyMissed = await web.GetStringAsync($"/Flashcards/Study?deckId={deck.Id}&missed=true");
            Assert.Contains("không còn thẻ nào", WebUtility.HtmlDecode(emptyMissed));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static FormUrlEncodedContent Form(string html, params (string Key, string Value)[] fields)
    {
        var values = fields.ToDictionary(field => field.Key, field => field.Value);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return new FormUrlEncodedContent(values);
    }
}
