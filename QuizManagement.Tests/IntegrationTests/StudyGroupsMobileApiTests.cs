using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizManagement.Tests.ApiTests;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class StudyGroupsMobileApiTests
{
    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task QuizReports_ValidateOwnedHistoryAndPreventDuplicatePendingReports()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            using var host = Host(options);
            var student = host.AddUser("student@reports.local");
            var stranger = host.AddUser("stranger@reports.local");
            db.Users.AddRange(student, stranger);
            var subject = new Subject { Name = "Report subject", UserId = student.Id, CreatedAt = DateTime.UtcNow };
            var deck = new Deck { Name = "Report deck", Subject = subject, CreatedAt = DateTime.UtcNow };
            var question = new Question { Content = "Question", Deck = deck, QuestionType = 1, CreatedAt = DateTime.UtcNow };
            db.Questions.Add(question);
            await db.SaveChangesAsync();
            var history = new TestHistory { UserId = student.Id, DeckId = deck.Id, Score = 10, Percentage = 100,
                CreatedAt = DateTime.UtcNow, ResultSnapshotJson = JsonSerializer.Serialize(new QuizResultSnapshot
                {
                    DeckName = deck.Name, SubjectName = subject.Name,
                    Questions = [new QuizResultQuestionSnapshot { QuestionId = question.Id, Content = question.Content }]
                }) };
            db.TestHistories.Add(history);
            await db.SaveChangesAsync();
            using var a = await Client(host, student.Email);
            using var b = await Client(host, stranger.Email);
            var valid = new { testHistoryId = history.Id, questionId = question.Id, reason = "WrongAnswer", note = "Check answer" };
            await Error(await b.PostAsJsonAsync("/api/quiz/reports", valid), HttpStatusCode.NotFound);
            await Error(await a.PostAsJsonAsync("/api/quiz/reports", new { testHistoryId = history.Id, questionId = question.Id, reason = "invalid" }), HttpStatusCode.BadRequest);
            await Error(await a.PostAsJsonAsync("/api/quiz/reports", new { testHistoryId = history.Id, questionId = question.Id + 1, reason = "WrongAnswer" }), HttpStatusCode.NotFound);
            (await a.PostAsJsonAsync("/api/quiz/reports", valid)).EnsureSuccessStatusCode();
            await Error(await a.PostAsJsonAsync("/api/quiz/reports", valid), HttpStatusCode.Conflict);
            Assert.Single(await db.QuestionReports.AsNoTracking().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task Management_EnforcesRolesAndMembershipAcrossHttpRequests()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            using var host = Host(options);
            var owner = host.AddUser("owner@mobile.local");
            var member = host.AddUser("member@mobile.local");
            var outsider = host.AddUser("outsider@mobile.local");
            db.Users.AddRange(owner, member, outsider);
            await db.SaveChangesAsync();
            using var a = await Client(host, owner.Email);
            using var b = await Client(host, member.Email);
            using var c = await Client(host, outsider.Email);
            var created = await a.PostAsJsonAsync("/api/groups", new { name = "Mobile approval", accessMode = 1 });
            created.EnsureSuccessStatusCode();
            var id = (await Json(created)).GetProperty("id").GetInt32();
            var root = $"/api/groups/{id}";
            await Error(await b.GetAsync(root + "/management"), HttpStatusCode.Forbidden);
            Assert.Equal(HttpStatusCode.Accepted, (await b.PostAsJsonAsync(root + "/join", new { })).StatusCode);
            var requests = (await Json(await a.GetAsync(root + "/management"))).GetProperty("requests");
            Assert.Equal(member.Id, Assert.Single(requests.EnumerateArray()).GetProperty("userId").GetString());
            (await a.PostAsJsonAsync(root + $"/requests/{member.Id}/review", new { accept = true })).EnsureSuccessStatusCode();
            Assert.Equal(0, (await Json(await b.GetAsync(root))).GetProperty("myRole").GetInt32());
            await Error(await b.PutAsJsonAsync(root + "/access", new { accessMode = 0 }), HttpStatusCode.Forbidden);
            await Error(await b.PostAsJsonAsync(root + "/invite-links", new { durationDays = 7 }), HttpStatusCode.Forbidden);
            await Error(await b.PostAsJsonAsync(root + "/ownership", new { userId = member.Id }), HttpStatusCode.Forbidden);
            (await a.PutAsJsonAsync(root + $"/members/{member.Id}/moderator", new { moderator = true })).EnsureSuccessStatusCode();
            (await b.PostAsJsonAsync(root + "/invitations", new { email = outsider.Email })).EnsureSuccessStatusCode();
            Assert.Single((await Json(await c.GetAsync("/api/groups/invitations"))).EnumerateArray());
            (await c.PostAsJsonAsync(root + "/invitation-response", new { accept = true })).EnsureSuccessStatusCode();
            await Error(await b.DeleteAsync(root + $"/members/{owner.Id}"), HttpStatusCode.Forbidden);
            (await b.DeleteAsync(root + $"/members/{outsider.Id}")).EnsureSuccessStatusCode();
            await Error(await c.GetAsync(root + "/messages"), HttpStatusCode.NotFound);
            await Error(await c.PostAsJsonAsync(root + "/join", new { }), HttpStatusCode.Forbidden);
            (await b.DeleteAsync(root + $"/bans/{outsider.Id}")).EnsureSuccessStatusCode();
            (await a.PostAsJsonAsync(root + "/ownership", new { userId = member.Id })).EnsureSuccessStatusCode();
            await Error(await a.PutAsJsonAsync(root + "/access", new { accessMode = 0 }), HttpStatusCode.Forbidden);
            (await a.PostAsJsonAsync(root + "/leave", new { })).EnsureSuccessStatusCode();
            await Error(await b.PostAsJsonAsync(root + "/leave", new { }), HttpStatusCode.Forbidden);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task InviteLinks_WorkForPrivateRoomsAndBecomeUnavailableAfterRevocationOrModeChange()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            using var host = Host(options);
            var owner = host.AddUser("owner@links.local");
            var member = host.AddUser("member@links.local");
            db.Users.AddRange(owner, member);
            await db.SaveChangesAsync();
            using var a = await Client(host, owner.Email);
            using var b = await Client(host, member.Email);
            var id = (await Json(await a.PostAsJsonAsync("/api/groups", new { name = "Private mobile", accessMode = 2 }))).GetProperty("id").GetInt32();
            var root = $"/api/groups/{id}";
            Assert.Empty((await Json(await b.GetAsync("/api/groups/discovery"))).GetProperty("groups").EnumerateArray());
            await Error(await a.PostAsJsonAsync(root + "/invite-links", new { durationDays = -1 }), HttpStatusCode.BadRequest);
            var token = (await Json(await a.PostAsJsonAsync(root + "/invite-links", new { durationDays = 7 }))).GetProperty("token").GetString();
            var preview = await b.GetAsync($"/api/groups/invite/{token}");
            preview.EnsureSuccessStatusCode();
            Assert.True(preview.Headers.CacheControl?.NoStore);
            (await b.PostAsJsonAsync($"/api/groups/invite/{token}", new { })).EnsureSuccessStatusCode();
            var manage = await a.GetAsync(root + "/management");
            Assert.True(manage.Headers.CacheControl?.NoStore);
            var linkId = Assert.Single((await Json(manage)).GetProperty("inviteLinks").EnumerateArray()).GetProperty("id").GetInt32();
            (await a.DeleteAsync(root + $"/invite-links/{linkId}")).EnsureSuccessStatusCode();
            await Error(await b.PostAsJsonAsync($"/api/groups/invite/{token}", new { }), HttpStatusCode.Gone);
            var second = (await Json(await a.PostAsJsonAsync(root + "/invite-links", new { durationDays = 0 }))).GetProperty("token").GetString();
            (await a.PutAsJsonAsync(root + "/access", new { accessMode = 0 })).EnsureSuccessStatusCode();
            await Error(await b.GetAsync($"/api/groups/invite/{second}"), HttpStatusCode.Gone);
            Assert.Equal(2, (await Json(await b.GetAsync(root))).GetProperty("members").GetArrayLength());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static ApiTestHost Host(DbContextOptions<QuizManagementDbContext> options) => new()
    {
        ConfigureServices = services =>
        {
            services.RemoveAll<QuizManagementDbContext>();
            services.AddScoped(_ => new QuizManagementDbContext(options));
        }
    };
    private static async Task<HttpClient> Client(ApiTestHost host, string email)
    {
        var client = host.CreateClient(new() { AllowAutoRedirect = false });
        ApiTestHost.UseToken(client, await host.LoginAsync(client, email));
        return client;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("code", out _));
    }
}
