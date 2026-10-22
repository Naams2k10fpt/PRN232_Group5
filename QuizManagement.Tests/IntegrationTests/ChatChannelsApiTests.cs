using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizManagement.Services;
using QuizManagement.Tests.ApiTests;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public sealed class ChatChannelsApiTests
{
    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task Migration_PreservesExistingMessagesAndReadPositions()
    {
        var options = Options();
        await using var db = new QuizManagementDbContext(options);
        try
        {
            const string previous = "20261007174822_AddStudyGroupInviteLinks";
            await db.GetService<IMigrator>().MigrateAsync(previous);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Users" ("Id", "Username", "Email", "Role", "EmailConfirmed", "IsDisabled", "CreatedAt")
                VALUES ('old-owner', 'Old owner', 'old@migration.local', 'User', true, false, now());
                INSERT INTO "StudyGroups" ("Name", "CreatedByUserId", "CreatedAt") VALUES ('Existing room', 'old-owner', now());
                INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "Role", "JoinedAt")
                SELECT "Id", 'old-owner', 2, now() FROM "StudyGroups";
                INSERT INTO "GroupMessages" ("GroupId", "SenderUserId", "Content", "SentAt")
                SELECT "Id", 'old-owner', 'Keep this history', now() FROM "StudyGroups";
                UPDATE "StudyGroupMembers" SET "LastReadMessageId" = (SELECT max("Id") FROM "GroupMessages");
                """);
            await db.Database.MigrateAsync();
            var channel = await db.ChatChannels.SingleAsync(item => item.Kind == ChatChannelKind.Text);
            Assert.True((await db.ChatChannels.SingleAsync(item => item.Kind == ChatChannelKind.Voice)).IsDefault);
            var message = await db.GroupMessages.SingleAsync();
            Assert.True(channel.IsDefault);
            Assert.Equal("chung", channel.Name);
            Assert.Equal(channel.Id, message.ChannelId);
            Assert.Equal("Keep this history", message.Content);
            Assert.Equal(message.Id, (await db.ChatChannelReads.SingleAsync()).LastReadMessageId);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task Channels_Replies_Reactions_Files_AndModeration_EnforceMembership()
    {
        var options = Options();
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            using var host = Host(options);
            var owner = host.AddUser("owner@channels.local");
            var member = host.AddUser("member@channels.local");
            var outsider = host.AddUser("outside@channels.local");
            db.Users.AddRange(owner, member, outsider);
            await db.SaveChangesAsync();
            using var a = await Client(host, owner.Email);
            using var b = await Client(host, member.Email);
            using var c = await Client(host, outsider.Email);
            var group = await Json(await a.PostAsJsonAsync("/api/groups", new { name = "Study server" }));
            var root = $"/api/groups/{group.GetProperty("id").GetInt32()}";
            (await b.PostAsJsonAsync(root + "/join", new { })).EnsureSuccessStatusCode();
            var channels = await Json(await a.GetAsync(root + "/channels"));
            var general = channels[0].GetProperty("id").GetInt32();
            Assert.True(channels[0].GetProperty("isDefault").GetBoolean());
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(root + "/channels")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await b.PostAsJsonAsync(root + "/channels", new { name = "blocked" })).StatusCode);
            var channel = await Json(await a.PostAsJsonAsync(root + "/channels", new { name = "Bài tập", topic = "Hỏi đáp" }));
            var channelId = channel.GetProperty("id").GetInt32();
            var path = root + $"/channels/{channelId}/messages";
            Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(root + "/channels", new { name = "Bài tập" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await a.DeleteAsync(root + $"/channels/{general}")).StatusCode);

            var nonce = Guid.NewGuid();
            var first = await Send(a, path, "Gửi bài tập", nonce, file: "notes.txt");
            var id = first.GetProperty("id").GetInt32();
            Assert.Equal(id, (await Send(a, path, "Gửi bài tập", nonce)).GetProperty("id").GetInt32());
            var fileId = first.GetProperty("attachments")[0].GetProperty("id").GetGuid();
            var fileUrl = $"/api/chat/attachments/{fileId}";
            Assert.Equal("study notes", await b.GetStringAsync(fileUrl));
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(fileUrl)).StatusCode);
            var reply = await Send(b, path, "Đã nhận", Guid.NewGuid(), reply: id);
            Assert.Equal("Gửi bài tập", reply.GetProperty("reply").GetProperty("content").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync(root + $"/channels/{general}/messages", Form("Cross channel", Guid.NewGuid(), id))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await b.PutAsJsonAsync(path + $"/{id}", new { content = "Hijack" })).StatusCode);
            var edited = await Json(await a.PutAsJsonAsync(path + $"/{id}", new { content = "Bài tập đã cập nhật" }));
            Assert.NotEqual(JsonValueKind.Null, edited.GetProperty("editedAt").ValueKind);
            Assert.True(edited.GetProperty("revision").GetInt32() > 0);
            await Json(await b.PutAsJsonAsync(path + $"/{id}/reaction", new { emoji = "👍", active = true }));
            var reacted = await Json(await b.PutAsJsonAsync(path + $"/{id}/reaction", new { emoji = "👍", active = true }));
            Assert.Single(reacted.GetProperty("reactions").EnumerateArray());
            Assert.Equal(HttpStatusCode.Forbidden, (await b.DeleteAsync(path + $"/{id}")).StatusCode);
            var deleted = await Json(await a.DeleteAsync(path + $"/{id}"));
            Assert.Equal("", deleted.GetProperty("content").GetString());
            Assert.Empty(deleted.GetProperty("attachments").EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync(fileUrl)).StatusCode);
            var history = await Json(await b.GetAsync(path));
            Assert.True(history[1].GetProperty("reply").GetProperty("isDeleted").GetBoolean());
            (await a.DeleteAsync(root + $"/channels/{channelId}")).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync(path)).StatusCode);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task CatchUp_PagesForward_AndReadCursorsNeverMoveBackwards()
    {
        var options = Options();
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            using var host = Host(options);
            var owner = host.AddUser("author@history.local");
            var reader = host.AddUser("reader@history.local");
            db.Users.AddRange(owner, reader);
            await db.SaveChangesAsync();
            using var a = await Client(host, owner.Email);
            using var b = await Client(host, reader.Email);
            var groupId = (await Json(await a.PostAsJsonAsync("/api/groups", new { name = "History" }))).GetProperty("id").GetInt32();
            var root = $"/api/groups/{groupId}/channels";
            (await b.PostAsJsonAsync($"/api/groups/{groupId}/join", new { })).EnsureSuccessStatusCode();
            var channelId = (await Json(await a.GetAsync(root)))[0].GetProperty("id").GetInt32();
            var groups = new StudyGroupService(db, TimeProvider.System, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            var service = new ChatService(db, groups, TimeProvider.System);
            var ids = new List<int>();
            for (var index = 0; index < 72; index++)
                ids.Add((await service.SendAsync(groupId, channelId, owner.Id, $"Message {index}")).Id);
            var page1 = await Json(await b.GetAsync(root + $"/{channelId}/messages?afterId={ids[0]}&limit=30"));
            var page2 = await Json(await b.GetAsync(root + $"/{channelId}/messages?afterId={page1[29].GetProperty("id").GetInt32()}&limit=30"));
            var page3 = await Json(await b.GetAsync(root + $"/{channelId}/messages?afterId={page2[29].GetProperty("id").GetInt32()}&limit=30"));
            Assert.Equal(71, page1.GetArrayLength() + page2.GetArrayLength() + page3.GetArrayLength());
            var read = root + $"/{channelId}/read";
            (await b.PostAsJsonAsync(read, new { lastMessageId = ids[^1] })).EnsureSuccessStatusCode();
            (await b.PostAsJsonAsync(read, new { lastMessageId = ids[0] })).EnsureSuccessStatusCode();
            Assert.Equal(0, (await Json(await b.GetAsync(root)))[0].GetProperty("unreadCount").GetInt32());
            Assert.Equal(ids[^1], await db.ChatChannelReads.Where(item => item.ChannelId == channelId && item.UserId == reader.Id)
                .Select(item => item.LastReadMessageId).SingleAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData("payload.html"), InlineData("payload.svg"), InlineData("fake.png")]
    public async Task UploadPolicy_RejectsActiveContentAndForgedImages(string name)
    {
        var bytes = Encoding.UTF8.GetBytes("<script>alert('x')</script>");
        using var stream = new MemoryStream(bytes);
        var file = new Microsoft.AspNetCore.Http.FormFile(stream, 0, bytes.Length, "files", name);
        await Assert.ThrowsAsync<ChatFailure>(() => ChatFilePolicy.ReadAsync(file, CancellationToken.None));
    }

    private static DbContextOptions<QuizManagementDbContext> Options() => new DbContextOptionsBuilder<QuizManagementDbContext>()
        .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
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
    private static MultipartFormDataContent Form(string content, Guid nonce, int? reply = null, string? file = null)
    {
        var form = new MultipartFormDataContent { { new StringContent(content), "content" }, { new StringContent(nonce.ToString()), "clientMessageId" } };
        if (reply.HasValue) form.Add(new StringContent(reply.ToString()!), "replyToId");
        if (file is not null) form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("study notes")), "files", file);
        return form;
    }
    private static async Task<JsonElement> Send(HttpClient client, string path, string content, Guid nonce, int? reply = null, string? file = null)
    {
        using var form = Form(content, nonce, reply, file);
        return await Json(await client.PostAsync(path, form));
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }
}
