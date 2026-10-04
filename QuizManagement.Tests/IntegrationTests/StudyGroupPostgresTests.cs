using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using QuizManagement.Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public sealed class StudyGroupPostgresTests
{
    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Chat_PersistsMessagesAndKeepsThemWithinTheGroup()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName))
            .Options;

        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new User { Id = "alice", Username = "Alice", Email = "alice@test.local",
                    Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "bob", Username = "Bob", Email = "bob@test.local",
                    Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var chat = new StudyGroupService(db, TimeProvider.System);
            var group = await chat.CreateAsync("alice", "Ôn tập", null, null);
            Assert.NotNull(group);
            Assert.Single(await chat.ListAsync("alice"));
            Assert.Empty(await chat.ListAsync("bob"));
            Assert.Null(await chat.MessagesAsync(group.Id, "bob"));
            Assert.Null(await chat.SendAsync(group.Id, "bob", "Không được gửi"));

            Assert.True(await chat.JoinAsync(group.Id, "bob"));
            var first = await chat.SendAsync(group.Id, "alice", "Câu hỏi đầu tiên");
            Assert.NotNull(first);
            Assert.Equal(1, Assert.Single(await chat.ListAsync("bob")).UnreadCount);
            Assert.Equal(first.Id, Assert.Single((await chat.MessagesAsync(group.Id, "bob"))!).Id);
            Assert.True(await chat.MarkReadAsync(group.Id, "bob", first.Id));
            Assert.Equal(0, Assert.Single(await chat.ListAsync("bob")).UnreadCount);

            var second = await chat.SendAsync(group.Id, "alice", "Câu hỏi thứ hai");
            Assert.NotNull(second);
            var older = await chat.MessagesAsync(group.Id, "bob", second.Id, 1);
            Assert.Equal(first.Id, Assert.Single(older!).Id);

            db.ChangeTracker.Clear();
            Assert.Equal(1, Assert.Single(await chat.ListAsync("bob")).UnreadCount);
            Assert.Equal(2, (await chat.MessagesAsync(group.Id, "bob"))!.Count);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
