using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Services;
using QuizManagement.Controllers;
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
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "bob", Username = "Bob", Email = "bob@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var chat = new StudyGroupService(db, TimeProvider.System, new EphemeralDataProtectionProvider());
            var group = await chat.CreateAsync("alice", "Ôn tập", null, null);
            Assert.NotNull(group);
            Assert.Single(await chat.ListAsync("alice"));
            Assert.Empty(await chat.ListAsync("bob"));
            Assert.Null(await chat.MessagesAsync(group.Id, "bob"));
            Assert.Null(await chat.SendAsync(group.Id, "bob", "Không được gửi"));

            Assert.Equal(GroupJoinResult.Joined, await chat.JoinAsync(group.Id, "bob"));
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

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task MembershipEvents_AppearInHistoryWithoutIncreasingUnreadChatCount()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName)).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new User { Id = "alice", Username = "Alice", Email = "alice@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "bob", Username = "Bob", Email = "bob@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var chat = new StudyGroupService(db, TimeProvider.System, new EphemeralDataProtectionProvider());
            var group = (await chat.CreateAsync("alice", "Ôn tập", null, null))!;

            Assert.Equal(GroupJoinResult.Joined, await chat.JoinAsync(group.Id, "bob"));
            var joined = await chat.RecordMembershipEventAsync(group.Id, "bob", GroupMessageKind.Joined);
            Assert.Equal(0, Assert.Single(await chat.ListAsync("alice")).UnreadCount);
            Assert.Equal(GroupActionResult.Success, await chat.LeaveAsync(group.Id, "bob"));
            var left = await chat.RecordMembershipEventAsync(group.Id, "bob", GroupMessageKind.Left);

            var history = (await chat.MessagesAsync(group.Id, "alice"))!;
            Assert.Equal([GroupMessageKind.Joined, GroupMessageKind.Left], history.Select(message => message.Kind));
            Assert.Equal([joined.Id, left.Id], history.Select(message => message.Id));
            Assert.Equal(0, Assert.Single(await chat.ListAsync("alice")).UnreadCount);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task RestrictedRooms_RequireApprovalOrInvitationAndStopDeliveringAfterRemoval()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName)).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new User { Id = "owner", Username = "Owner", Email = "owner@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "member", Username = "Member", Email = "member@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "outsider", Username = "Outsider", Email = "outsider@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var chat = new StudyGroupService(db, TimeProvider.System, new EphemeralDataProtectionProvider());
            var approval = (await chat.CreateAsync("owner", "Cần duyệt", null, null,
                StudyGroupAccessMode.PublicApproval))!;
            Assert.Equal(GroupJoinResult.PendingApproval, await chat.JoinAsync(approval.Id, "member"));
            Assert.Null(await chat.MessagesAsync(approval.Id, "member"));
            Assert.Equal(GroupActionResult.Forbidden,
                await chat.ReviewRequestAsync(approval.Id, "outsider", "member", true));
            Assert.Equal(GroupActionResult.Success,
                await chat.ReviewRequestAsync(approval.Id, "owner", "member", true));
            Assert.True(await chat.IsMemberAsync(approval.Id, "member"));

            var privateRoom = (await chat.CreateAsync("owner", "Bí mật", null, null,
                StudyGroupAccessMode.PrivateInviteOnly))!;
            Assert.DoesNotContain(await chat.ListAsync("outsider", discover: true),
                group => group.Id == privateRoom.Id);
            Assert.Equal(GroupJoinResult.InvitationRequired,
                await chat.JoinAsync(privateRoom.Id, "member"));
            Assert.Null(await chat.MessagesAsync(privateRoom.Id, "member"));
            Assert.Equal(GroupActionResult.Forbidden,
                await chat.InviteAsync(privateRoom.Id, "outsider", "member@test.local"));
            Assert.Equal(GroupActionResult.Success,
                await chat.InviteAsync(privateRoom.Id, "owner", "member@test.local"));
            Assert.Single(await chat.InvitationsAsync("member"));
            Assert.Equal(GroupActionResult.Success,
                await chat.RespondInvitationAsync(privateRoom.Id, "member", true));
            Assert.True(await chat.IsMemberAsync(privateRoom.Id, "member"));
            Assert.Contains("member", await chat.RecipientIdsAsync(privateRoom.Id));

            var disabledUser = await db.Users.FindAsync("member");
            disabledUser!.IsDisabled = true;
            await db.SaveChangesAsync();
            Assert.False(await chat.IsMemberAsync(privateRoom.Id, "member"));
            Assert.Null(await chat.SendAsync(privateRoom.Id, "member", "Không thể gửi khi bị khóa"));
            Assert.DoesNotContain("member", await chat.RecipientIdsAsync(privateRoom.Id));
            disabledUser.IsDisabled = false;
            await db.SaveChangesAsync();

            Assert.Equal(GroupActionResult.Success,
                await chat.RemoveMemberAsync(privateRoom.Id, "owner", "member"));
            Assert.False(await chat.IsMemberAsync(privateRoom.Id, "member"));
            Assert.DoesNotContain("member", await chat.RecipientIdsAsync(privateRoom.Id));
            Assert.Null(await chat.SendAsync(privateRoom.Id, "member", "Tin sau khi bị loại"));
            Assert.Equal(GroupJoinResult.Banned, await chat.JoinAsync(privateRoom.Id, "member"));
            Assert.DoesNotContain(await chat.ListAsync("member", discover: true),
                group => group.Id == privateRoom.Id);

            Assert.Equal(GroupActionResult.Success,
                await chat.SetModeratorAsync(approval.Id, "owner", "member", true));
            Assert.Equal(GroupActionResult.Forbidden,
                await chat.RemoveMemberAsync(approval.Id, "member", "owner"));
            Assert.Equal(GroupActionResult.Success,
                await chat.TransferOwnershipAsync(approval.Id, "owner", "member"));
            Assert.True(await chat.IsOwnerAsync(approval.Id, "member"));
            Assert.False(await chat.IsOwnerAsync(approval.Id, "owner"));
            Assert.Equal("Member", (await chat.ListAsync("member")).Single(group => group.Id == approval.Id).OwnerName);
            Assert.Equal(GroupActionResult.Forbidden, await chat.LeaveAsync(approval.Id, "member"));
            Assert.Equal(GroupActionResult.Success, await chat.LeaveAsync(approval.Id, "owner"));

            var pending = (await chat.CreateAsync("owner", "Đổi chế độ", null, null,
                StudyGroupAccessMode.PublicApproval))!;
            Assert.Equal(GroupJoinResult.PendingApproval, await chat.JoinAsync(pending.Id, "outsider"));
            Assert.Equal(GroupActionResult.Success,
                await chat.CancelRequestAsync(pending.Id, "outsider"));
            Assert.Empty((await chat.PendingRequestsAsync(pending.Id, "owner"))!);
            Assert.Equal(GroupJoinResult.PendingApproval, await chat.JoinAsync(pending.Id, "outsider"));
            Assert.Equal(GroupActionResult.Success,
                await chat.ChangeAccessModeAsync(pending.Id, "owner",
                    StudyGroupAccessMode.PrivateInviteOnly));
            Assert.Empty((await chat.PendingRequestsAsync(pending.Id, "owner"))!);
            Assert.Equal(GroupJoinResult.InvitationRequired,
                await chat.JoinAsync(pending.Id, "outsider"));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Directory_FiltersPublicRoomsWithoutExposingPrivateOrJoinedGroups()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName)).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new User { Id = "owner", Username = "Owner", Email = "owner@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "visitor", Username = "Visitor", Email = "visitor@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var subject = new Subject { UserId = "owner", Name = "Toán", CreatedAt = DateTime.UtcNow };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync();

            var chat = new StudyGroupService(db, TimeProvider.System, new EphemeralDataProtectionProvider());
            Assert.Null(await chat.CreateAsync("visitor", "Không hợp lệ", null, subject.Id));
            var open = (await chat.CreateAsync("owner", "Đại số", "Ôn công thức", subject.Id))!;
            var approval = (await chat.CreateAsync("owner", "Hình học", "Bài tập toán", subject.Id,
                StudyGroupAccessMode.PublicApproval))!;
            var privateRoom = (await chat.CreateAsync("owner", "Riêng tư", null, null,
                StudyGroupAccessMode.PrivateInviteOnly))!;

            var publicPage = await chat.DiscoverPageAsync("visitor", null, null, null, null, 1, 1);
            Assert.Equal(2, publicPage.TotalCount);
            Assert.Equal(2, publicPage.PageCount);
            Assert.Single(publicPage.Groups);
            Assert.DoesNotContain(privateRoom.Id, publicPage.Groups.Select(group => group.Id));
            Assert.Equal(approval.Id, Assert.Single((await chat.DiscoverPageAsync("visitor", "BÀI TẬP",
                StudyGroupAccessMode.PublicApproval, subject.Id, "newest", 1)).Groups).Id);
            Assert.Equal(open.Id, Assert.Single((await chat.DiscoverPageAsync("visitor", "ĐẠI SỐ",
                StudyGroupAccessMode.PublicOpen, subject.Id, "popular", 1)).Groups).Id);
            Assert.Equal(subject.Id, Assert.Single(await chat.PublicSubjectsAsync("visitor")).Id);
            var ownerGroups = await chat.ListAsync("owner");
            Assert.Contains(ownerGroups, group => group.Id == privateRoom.Id);

            Assert.Equal(GroupJoinResult.Joined, await chat.JoinAsync(open.Id, "visitor"));
            var remaining = await chat.DiscoverPageAsync("visitor", null, null, null, null, 1);
            Assert.Equal(1, remaining.TotalCount);
            Assert.Equal(approval.Id, Assert.Single(remaining.Groups).Id);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task AccessMigration_PreservesExistingRoomAndPromotesItsCreator()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName)).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20261004090814_AddStudyGroupChat");
            db.Users.Add(new User { Id = "creator", Username = "Creator",
                Email = "creator@test.local", Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "StudyGroups" ("Id", "Name", "CreatedByUserId", "CreatedAt")
                VALUES (777, 'Existing room', 'creator', now());
                INSERT INTO "StudyGroupMembers" ("GroupId", "UserId", "JoinedAt")
                VALUES (777, 'creator', now());
                """);

            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var group = await db.StudyGroups.SingleAsync(group => group.Id == 777);
            var member = await db.StudyGroupMembers.SingleAsync(member => member.GroupId == 777);
            Assert.Equal(StudyGroupAccessMode.PublicOpen, group.AccessMode);
            Assert.Equal(StudyGroupMemberRole.Owner, member.Role);
            Assert.False(member.IsBanned);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task InviteLinks_OfferFourDurationsAndOwnerCanManageEachLink()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName)).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new User { Id = "owner", Username = "Owner", Email = "owner@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow },
                new User { Id = "member", Username = "Member", Email = "member@test.local",
                    EmailConfirmed = true, Role = AppRoles.User, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var protectionProvider = new EphemeralDataProtectionProvider();
            var chat = new StudyGroupService(db, TimeProvider.System, protectionProvider);
            var group = (await chat.CreateAsync("owner", "Nhóm riêng", null, null,
                StudyGroupAccessMode.PrivateInviteOnly))!;

            Assert.Null(await chat.CreateInviteLinkAsync(group.Id, "member", 7));
            Assert.Null(await chat.CreateInviteLinkAsync(group.Id, "owner", 2));
            var tokens = new List<string>();
            foreach (var days in new[] { 1, 7, 30, 0 })
            {
                var token = await chat.CreateInviteLinkAsync(group.Id, "owner", days);
                Assert.NotNull(token);
                tokens.Add(token);
                var preview = await chat.InviteLinkPreviewAsync(token);
                Assert.NotNull(preview);
                if (days == 0) Assert.Null(preview.ExpiresAt);
                else Assert.InRange((preview.ExpiresAt!.Value - DateTime.UtcNow).TotalDays,
                    days - 0.01, days + 0.01);
            }

            var links = await chat.InviteLinksAsync(group.Id, "owner");
            Assert.Equal(4, links.Count);
            Assert.Equal(tokens.Order(), links.Select(link => link.Token!).Order());
            await using (var refreshedDb = new QuizManagementDbContext(options))
            {
                var refreshedChat = new StudyGroupService(refreshedDb, TimeProvider.System,
                    protectionProvider);
                Assert.Equal(tokens.Order(), (await refreshedChat.InviteLinksAsync(group.Id, "owner"))
                    .Select(link => link.Token!).Order());
            }
            Assert.Empty(await chat.InviteLinksAsync(group.Id, "member"));
            Assert.All(await db.StudyGroupInviteLinks.ToListAsync(), link =>
                Assert.DoesNotContain(link.ProtectedToken, tokens));

            var firstLinkId = links.Single(link => link.Token == tokens[0]).Id;
            Assert.Equal(GroupActionResult.Forbidden,
                await chat.RevokeInviteLinkAsync(group.Id, firstLinkId, "member"));
            Assert.Equal(GroupActionResult.Success,
                await chat.RevokeInviteLinkAsync(group.Id, firstLinkId, "owner"));
            Assert.Null(await chat.InviteLinkPreviewAsync(tokens[0]));
            Assert.Equal(GroupInviteLinkFailure.Revoked,
                await chat.InviteLinkFailureAsync(tokens[0]));
            var revokedController = new StudyGroupsController(chat, null!)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var revokedPage = Assert.IsType<ViewResult>(await revokedController.InviteLink(tokens[0]));
            Assert.Equal("InviteLinkUnavailable", revokedPage.ViewName);
            Assert.Equal(GroupInviteLinkFailure.Revoked, revokedPage.Model);
            Assert.Equal(StatusCodes.Status410Gone, revokedController.Response.StatusCode);
            Assert.NotNull(await chat.InviteLinkPreviewAsync(tokens[1]));

            Assert.Equal(GroupJoinResult.Joined,
                await chat.JoinWithInviteLinkAsync(tokens[2], "member"));
            Assert.Equal(GroupJoinResult.AlreadyMember,
                await chat.JoinWithInviteLinkAsync(tokens[2], "member"));
            Assert.True(await chat.IsMemberAsync(group.Id, "member"));
            Assert.Equal(GroupActionResult.Success,
                await chat.RemoveMemberAsync(group.Id, "owner", "member"));
            Assert.Equal(GroupJoinResult.Banned,
                await chat.JoinWithInviteLinkAsync(tokens[3], "member"));

            var expiringLinkId = links.Single(item => item.Token == tokens[1]).Id;
            var expiringLink = await db.StudyGroupInviteLinks.SingleAsync(link =>
                link.Id == expiringLinkId);
            expiringLink.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            Assert.Null(await chat.InviteLinkPreviewAsync(tokens[1]));
            Assert.Equal(GroupInviteLinkFailure.Expired,
                await chat.InviteLinkFailureAsync(tokens[1]));
            var controller = new StudyGroupsController(chat, null!)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var expiredPage = Assert.IsType<ViewResult>(await controller.InviteLink(tokens[1]));
            Assert.Equal("InviteLinkUnavailable", expiredPage.ViewName);
            Assert.Equal(GroupInviteLinkFailure.Expired, expiredPage.Model);
            Assert.Equal(StatusCodes.Status410Gone, controller.Response.StatusCode);
            Assert.Equal(GroupInviteLinkFailure.Unknown,
                await chat.InviteLinkFailureAsync("invalid-token"));
            Assert.NotNull(await chat.InviteLinkPreviewAsync(tokens[3]));
            Assert.Equal(GroupActionResult.Success,
                await chat.ChangeAccessModeAsync(group.Id, "owner", StudyGroupAccessMode.PublicApproval));
            Assert.Null(await chat.InviteLinkPreviewAsync(tokens[3]));
            Assert.Equal(GroupInviteLinkFailure.Revoked,
                await chat.InviteLinkFailureAsync(tokens[3]));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
