using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuizManagement.Services;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public sealed class VoiceChannelMigrationTests
{
    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task UpgradeRepairsMissingDefaultsAndPreservesExistingChatHistory()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261022050153_AddDiscordStyleChat");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Users" ("Id", "Username", "Email", "Role", "EmailConfirmed", "IsDisabled", "CreatedAt")
                VALUES ('voice-owner', 'Voice owner', 'voice@migration.test', 'User', true, false, now());
                INSERT INTO "StudyGroups" ("Name", "CreatedByUserId", "CreatedAt")
                VALUES ('Preserved', 'voice-owner', now()), ('Missing', 'voice-owner', now()), ('Archived', 'voice-owner', now());
                INSERT INTO "ChatChannels" ("GroupId", "Name", "IsDefault", "IsArchived", "CreatedAt")
                SELECT "Id", CASE WHEN "Name" = 'Preserved' THEN 'renamed-general' ELSE 'chung' END,
                    "Name" = 'Preserved', "Name" = 'Archived', now()
                FROM "StudyGroups" WHERE "Name" <> 'Missing';
                INSERT INTO "GroupMessages" ("GroupId", "ChannelId", "SenderUserId", "Content", "SentAt")
                SELECT "GroupId", "Id", 'voice-owner', 'Keep the old chat', now() FROM "ChatChannels" WHERE "Name" = 'renamed-general';
                INSERT INTO "ChatChannelReads" ("ChannelId", "UserId", "LastReadMessageId")
                SELECT "ChannelId", 'voice-owner', "Id" FROM "GroupMessages";
                """);
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            var channels = await db.ChatChannels.AsNoTracking().ToListAsync();
            Assert.Equal(6, channels.Count);
            foreach (var group in await db.StudyGroups.ToListAsync())
            {
                Assert.Single(channels, channel => channel.GroupId == group.Id && channel.Kind == ChatChannelKind.Text && channel.IsDefault);
                Assert.Single(channels, channel => channel.GroupId == group.Id && channel.Kind == ChatChannelKind.Voice && channel.IsDefault);
            }
            Assert.All(channels, channel => Assert.False(channel.IsArchived));
            var message = await db.GroupMessages.SingleAsync();
            Assert.Equal("Keep the old chat", message.Content);
            Assert.Equal("renamed-general", channels.Single(channel => channel.Id == message.ChannelId).Name);
            Assert.Equal(message.Id, (await db.ChatChannelReads.SingleAsync()).LastReadMessageId);
            Assert.False(db.Database.HasPendingModelChanges());

            var groups = new StudyGroupService(db, TimeProvider.System, new EphemeralDataProtectionProvider());
            var created = (await groups.CreateAsync("voice-owner", "New group", null, null))!;
            Assert.Equal([ChatChannelKind.Text, ChatChannelKind.Voice],
                created.Channels.OrderBy(channel => channel.Kind).Select(channel => channel.Kind));
            Assert.All(created.Channels, channel => Assert.True(channel.IsDefault));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
