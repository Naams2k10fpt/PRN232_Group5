using System.Text.RegularExpressions;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace QuizManagement.Services;

public sealed record ChatReply(int Id, string SenderName, string Content, bool IsDeleted, int Revision = 0);
public sealed record ChatFile(Guid Id, string FileName, string ContentType, int Size);
public sealed record ReactionSummary(string Emoji, string UserId);
public sealed record ChannelSummary(int Id, int GroupId, string Name, string? Topic, bool IsDefault, int UnreadCount);
public sealed record ChatUpload(string FileName, string ContentType, byte[] Data);
public sealed class ChatFailure(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed class ChatService(QuizManagementDbContext db, StudyGroupService groups, TimeProvider clock)
{
    public static readonly string[] AllowedReactions = ["👍", "❤️", "😂", "🎉", "🤔", "👀", "✅", "❌"];

    private async Task RequireMember(int groupId, string userId)
    {
        if (!await groups.IsMemberAsync(groupId, userId))
            throw new ChatFailure("group_forbidden", "Bạn không còn quyền vào nhóm.", 403);
    }

    private async Task<ChatChannel> Channel(int groupId, int channelId, string userId)
    {
        await RequireMember(groupId, userId);
        return await db.ChatChannels.AsNoTracking().SingleOrDefaultAsync(channel =>
            channel.Id == channelId && channel.GroupId == groupId && !channel.IsArchived)
            ?? throw new ChatFailure("channel_not_found", "Kênh không tồn tại hoặc đã được lưu trữ.", 404);
    }

    public async Task<List<ChannelSummary>> ChannelsAsync(int groupId, string userId)
    {
        await RequireMember(groupId, userId);
        return await db.ChatChannels.AsNoTracking().Where(channel => channel.GroupId == groupId && !channel.IsArchived)
            .OrderByDescending(channel => channel.IsDefault).ThenBy(channel => channel.Id)
            .Select(channel => new ChannelSummary(channel.Id, channel.GroupId, channel.Name, channel.Topic,
                channel.IsDefault, channel.Messages.Count(message => message.Kind == GroupMessageKind.Chat
                    && message.DeletedAt == null && message.SenderUserId != userId
                    && !channel.Reads.Any(read => read.UserId == userId && read.LastReadMessageId >= message.Id))))
            .ToListAsync();
    }

    public async Task<ChannelSummary> SaveChannelAsync(int groupId, int? channelId, string userId, string name, string? topic)
    {
        if (!await groups.IsManagerAsync(groupId, userId))
            throw new ChatFailure("group_forbidden", "Chỉ chủ phòng hoặc người quản lý được quản lý kênh.", 403);
        name = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}-]+", "-").Trim('-');
        topic = topic?.Trim();
        if (name.Length is < 1 or > 40 || topic?.Length > 200)
            throw new ChatFailure("invalid_channel", "Tên kênh cần 1–40 ký tự, chủ đề tối đa 200 ký tự.");
        var channel = channelId.HasValue
            ? await db.ChatChannels.SingleOrDefaultAsync(item => item.Id == channelId && item.GroupId == groupId && !item.IsArchived)
                ?? throw new ChatFailure("channel_not_found", "Không tìm thấy kênh.", 404)
            : new ChatChannel { GroupId = groupId, CreatedAt = clock.GetUtcNow().UtcDateTime };
        if (!channelId.HasValue && await db.ChatChannels.CountAsync(item => item.GroupId == groupId && !item.IsArchived) >= 30)
            throw new ChatFailure("channel_limit", "Mỗi nhóm có tối đa 30 kênh đang hoạt động.");
        channel.Name = name;
        channel.Topic = topic;
        if (!channelId.HasValue) db.ChatChannels.Add(channel);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException error) when (DbExceptionClassifier.IsUniqueViolation(error))
        {
            db.ChangeTracker.Clear();
            throw new ChatFailure("channel_name_taken", "Tên kênh đã được sử dụng.", 409);
        }
        return new(channel.Id, groupId, channel.Name, channel.Topic, channel.IsDefault, 0);
    }

    public async Task ArchiveChannelAsync(int groupId, int channelId, string userId)
    {
        var channel = await Channel(groupId, channelId, userId);
        if (!await groups.IsManagerAsync(groupId, userId))
            throw new ChatFailure("group_forbidden", "Bạn không có quyền quản lý kênh.", 403);
        if (channel.IsDefault) throw new ChatFailure("default_channel", "Không thể lưu trữ kênh mặc định.");
        await db.ChatChannels.Where(item => item.Id == channelId).ExecuteUpdateAsync(setters =>
            setters.SetProperty(item => item.IsArchived, true));
    }

    public async Task<List<ChatMessage>> MessagesAsync(int groupId, int channelId, string userId,
        int? beforeId = null, int? afterId = null, int limit = 50)
    {
        await Channel(groupId, channelId, userId);
        if (beforeId.HasValue && afterId.HasValue)
            throw new ChatFailure("invalid_cursor", "Chỉ sử dụng một mốc phân trang.");
        var query = db.GroupMessages.Where(message => message.GroupId == groupId && message.ChannelId == channelId);
        if (beforeId.HasValue) query = query.Where(message => message.Id < beforeId);
        if (afterId.HasValue) query = query.Where(message => message.Id > afterId);
        query = afterId.HasValue ? query.OrderBy(message => message.Id) : query.OrderByDescending(message => message.Id);
        var result = await Project(query.Take(Math.Clamp(limit, 1, 100))).ToListAsync();
        return result.OrderBy(message => message.Id).ToList();
    }

    // Project metadata, never the bytea file body, into timeline/history queries.
    public static IQueryable<ChatMessage> Project(IQueryable<GroupMessage> query) => query.AsNoTracking().Select(message =>
        new ChatMessage(message.Id, message.GroupId, message.SenderUserId, message.SenderUser.Username,
            message.SenderUser.AvatarUrl, message.DeletedAt == null ? message.Content : "", message.SentAt, message.Kind,
            message.ChannelId, message.Revision, message.EditedAt, message.DeletedAt,
            message.ReplyToMessage == null ? null : new ChatReply(message.ReplyToMessage.Id,
                message.ReplyToMessage.SenderUser.Username,
                message.ReplyToMessage.DeletedAt == null ? message.ReplyToMessage.Content : "Tin nhắn đã bị xóa",
                message.ReplyToMessage.DeletedAt != null, message.ReplyToMessage.Revision),
            message.Attachments.Where(file => message.DeletedAt == null)
                .Select(file => new ChatFile(file.Id, file.FileName, file.ContentType, file.Size)).ToList(),
            message.Reactions.Where(reaction => message.DeletedAt == null)
                .Select(reaction => new ReactionSummary(reaction.Emoji, reaction.UserId)).ToList()));

    private Task<ChatMessage> Message(int id) => Project(db.GroupMessages.Where(message => message.Id == id)).SingleAsync();

    public async Task<ChatMessage> SendAsync(int groupId, int channelId, string userId, string? content,
        int? replyToId = null, Guid? clientMessageId = null, IReadOnlyList<ChatUpload>? files = null)
    {
        await Channel(groupId, channelId, userId);
        content = content?.Trim() ?? "";
        if (content.Length > 2000 || (content.Length == 0 && files?.Count is not > 0))
            throw new ChatFailure("invalid_message", "Nhập tin nhắn (tối đa 2000 ký tự) hoặc đính kèm file.");
        if (files?.Count > ChatFilePolicy.MaxFiles || files?.Any(file => file.Data.Length is < 1 or > ChatFilePolicy.MaxFileBytes) == true)
            throw new ChatFailure("invalid_attachment", "Tối đa 3 file, mỗi file tối đa 5 MB.");
        if (clientMessageId == Guid.Empty) clientMessageId = null;
        if (clientMessageId.HasValue)
        {
            var previous = await db.GroupMessages.AsNoTracking().FirstOrDefaultAsync(message =>
                message.SenderUserId == userId && message.ClientMessageId == clientMessageId);
            if (previous is not null)
            {
                if (previous.ChannelId != channelId) throw new ChatFailure("message_conflict", "Mã tin nhắn đã được sử dụng.", 409);
                return await Message(previous.Id);
            }
        }
        if (replyToId.HasValue && !await db.GroupMessages.AnyAsync(message => message.Id == replyToId
            && message.ChannelId == channelId && message.Kind == GroupMessageKind.Chat && message.DeletedAt == null))
            throw new ChatFailure("reply_not_found", "Tin nhắn được trả lời không còn tồn tại trong kênh.", 404);
        var message = new GroupMessage
        {
            GroupId = groupId, ChannelId = channelId, SenderUserId = userId, Content = content,
            ReplyToMessageId = replyToId, ClientMessageId = clientMessageId, SentAt = clock.GetUtcNow().UtcDateTime,
            Attachments = files?.Select(file => new ChatAttachment
            {
                FileName = file.FileName, ContentType = file.ContentType, Data = file.Data, Size = file.Data.Length
            }).ToList() ?? []
        };
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (files?.Count > 0)
        {
            // Bound durable storage per study group, including concurrent uploads.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(73133, {groupId})");
            var used = await db.ChatAttachments.Where(file => file.Message.GroupId == groupId).SumAsync(file => (long)file.Size);
            if (used + files.Sum(file => (long)file.Data.Length) > 100L * 1024 * 1024)
                throw new ChatFailure("attachment_quota", "Nhóm đã đạt giới hạn file 100 MB. Xóa tin đính kèm cũ để giải phóng dung lượng.", 409);
        }
        db.GroupMessages.Add(message);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException error) when (clientMessageId.HasValue && DbExceptionClassifier.IsUniqueViolation(error))
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            var previous = await db.GroupMessages.SingleAsync(item => item.SenderUserId == userId && item.ClientMessageId == clientMessageId);
            if (previous.ChannelId != channelId) throw new ChatFailure("message_conflict", "Mã tin nhắn đã được sử dụng.", 409);
            return await Message(previous.Id);
        }
        var result = await Message(message.Id);
        await transaction.CommitAsync();
        return result;
    }

    public async Task<ChatMessage> ChangeMessageAsync(int groupId, int channelId, int messageId, string userId,
        string? content = null, bool delete = false, string? emoji = null, bool react = true)
    {
        await Channel(groupId, channelId, userId);
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize edit/delete/reaction for one message, including its revision and broadcast payload.
        var message = await db.GroupMessages.FromSqlInterpolated($"SELECT * FROM \"GroupMessages\" WHERE \"Id\" = {messageId} FOR UPDATE")
            .SingleOrDefaultAsync(item => item.GroupId == groupId && item.ChannelId == channelId);
        if (message is null || message.Kind != GroupMessageKind.Chat || message.DeletedAt != null)
            throw new ChatFailure("message_not_found", "Tin nhắn không còn tồn tại.", 404);
        if (emoji is not null)
        {
            if (!AllowedReactions.Contains(emoji)) throw new ChatFailure("invalid_reaction", "Reaction không hợp lệ.");
            var existing = await db.ChatReactions.FindAsync(messageId, userId, emoji);
            if (react && existing is null) db.ChatReactions.Add(new() { MessageId = messageId, UserId = userId, Emoji = emoji });
            if (!react && existing is not null) db.ChatReactions.Remove(existing);
        }
        else
        {
            var own = message.SenderUserId == userId;
            if (!own && (!delete || !await groups.IsManagerAsync(groupId, userId)))
                throw new ChatFailure("message_forbidden", "Bạn chỉ được sửa tin của mình; người quản lý có thể xóa tin.", 403);
            if (delete)
            {
                message.DeletedAt = clock.GetUtcNow().UtcDateTime;
                message.Content = "";
                await db.ChatAttachments.Where(file => file.MessageId == messageId).ExecuteDeleteAsync();
                await db.ChatReactions.Where(reaction => reaction.MessageId == messageId).ExecuteDeleteAsync();
            }
            else
            {
                content = content?.Trim() ?? "";
                if (content.Length is < 1 or > 2000) throw new ChatFailure("invalid_message", "Nội dung cần 1–2000 ký tự.");
                message.Content = content;
                message.EditedAt = clock.GetUtcNow().UtcDateTime;
            }
        }
        message.Revision++;
        await db.SaveChangesAsync();
        var result = await Message(messageId);
        await transaction.CommitAsync();
        return result;
    }

    public async Task MarkReadAsync(int groupId, int channelId, string userId, int messageId)
    {
        await Channel(groupId, channelId, userId);
        if (!await db.GroupMessages.AnyAsync(message => message.Id == messageId && message.ChannelId == channelId))
            throw new ChatFailure("invalid_read_position", "Tin nhắn không thuộc kênh.");
        await AdvanceReadAsync(db, channelId, userId, messageId);
    }

    public static Task<int> AdvanceReadAsync(QuizManagementDbContext db, int channelId, string userId, int messageId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ChatChannelReads\" (\"ChannelId\", \"UserId\", \"LastReadMessageId\") VALUES ({channelId}, {userId}, {messageId}) ON CONFLICT (\"ChannelId\", \"UserId\") DO UPDATE SET \"LastReadMessageId\" = GREATEST(\"ChatChannelReads\".\"LastReadMessageId\", EXCLUDED.\"LastReadMessageId\")");

    public async Task<ChatAttachment> DownloadAsync(Guid fileId, string userId)
    {
        var location = await db.ChatAttachments.Where(item => item.Id == fileId && item.Message.DeletedAt == null)
            .Select(item => new { item.Message.GroupId, item.Message.ChannelId }).SingleOrDefaultAsync();
        if (location is null) throw new ChatFailure("attachment_not_found", "File không còn tồn tại.", 404);
        await Channel(location.GroupId, location.ChannelId, userId);
        return await db.ChatAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId && item.Message.DeletedAt == null)
            ?? throw new ChatFailure("attachment_not_found", "File không còn tồn tại.", 404);
    }
}
