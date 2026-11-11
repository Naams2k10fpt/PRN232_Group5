using System.Net;
using System.Net.Http.Json;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizManagement.Tests.ApiTests;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public sealed class CatalogFavoritesApiTests
{
    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task Favorites_AreSharedAcrossSessionsButIsolatedByAccount_AndLegacyImportRespectsRemoval()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}"))
            .Options;
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
            var owner = host.AddUser();
            var other = host.AddUser("other@example.com");
            var subject = new Subject { UserId = owner.Id, Name = "Study" };
            var deck = new Deck { Name = "Review" };
            var deletedDeck = new Deck { Name = "Deleted", IsDeleted = true };
            subject.Decks.Add(deck);
            subject.Decks.Add(deletedDeck);
            db.AddRange(owner, other, subject);
            await db.SaveChangesAsync();

            using var anon = host.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/favorites/subjects")).StatusCode);
            using var first = await SignIn(host, owner.Email);
            using var second = await SignIn(host, owner.Email);
            using var otherClient = await SignIn(host, other.Email);

            var writes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                first.PutAsJsonAsync($"/api/favorites/subjects/{subject.Id}", new { accountId = owner.Id, favorite = true })));
            Assert.All(writes, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            Assert.Equal([subject.Id], (await second.GetFromJsonAsync<int[]>("/api/favorites/subjects"))!);
            Assert.Empty((await otherClient.GetFromJsonAsync<int[]>("/api/favorites/subjects"))!);

            var denied = await otherClient.PutAsJsonAsync(
                $"/api/favorites/subjects/{subject.Id}", new { accountId = owner.Id, favorite = false });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal([subject.Id], (await second.GetFromJsonAsync<int[]>("/api/favorites/subjects"))!);

            var legacy = new { accountId = owner.Id, ids = new[] { deck.Id, deck.Id, deletedDeck.Id, int.MaxValue } };
            Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsJsonAsync("/api/favorites/decks/import", legacy)).StatusCode);
            Assert.Equal([deck.Id], (await second.GetFromJsonAsync<int[]>("/api/favorites/decks"))!);
            Assert.Equal(HttpStatusCode.NoContent, (await second.PutAsJsonAsync(
                $"/api/favorites/decks/{deck.Id}", new { accountId = owner.Id, favorite = false })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsJsonAsync("/api/favorites/decks/import", legacy)).StatusCode);
            Assert.Empty((await first.GetFromJsonAsync<int[]>("/api/favorites/decks"))!);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<HttpClient> SignIn(ApiTestHost host, string email)
    {
        var client = host.CreateClient();
        ApiTestHost.UseToken(client, await host.LoginAsync(client, email));
        return client;
    }
}
