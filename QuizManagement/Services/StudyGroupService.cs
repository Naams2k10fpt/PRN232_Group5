using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace QuizManagement.Services;

public sealed record GroupSummary(int Id, string Name, string? Description, int? SubjectId,
    string? SubjectName, int MemberCount, int UnreadCount, bool IsMember);
public sealed record ChatMessage(int Id, int GroupId, string SenderUserId, string SenderName,
    string? SenderAvatarUrl, string Content, DateTime SentAt);

public sealed class StudyGroupService
{
    private readonly QuizManagementDbContext _db;
    private readonly TimeProvider _clock;

    public StudyGroupService(QuizManagementDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<bool> IsMemberAsync(int groupId, string userId) =>
        _db.StudyGroupMembers.AnyAsync(member => member.GroupId == groupId && member.UserId == userId);

    public async Task<List<GroupSummary>> ListAsync(string userId, bool discover = false)
    {
        var query = _db.StudyGroups.AsNoTracking();
        if (!discover)
            query = query.Where(group => group.Members.Any(member => member.UserId == userId));

        return await query.OrderByDescending(group => group.CreatedAt)
            .Select(group => new GroupSummary(
                group.Id, group.Name, group.Description, group.SubjectId,
                group.Subject == null ? null : group.Subject.Name,
                group.Members.Count,
                group.Messages.Count(message =>
                    group.Members.Any(member => member.UserId == userId
                        && (member.LastReadMessageId == null || message.Id > member.LastReadMessageId)
                        && message.SenderUserId != userId)),
                group.Members.Any(member => member.UserId == userId)))
            .ToListAsync();
    }

    public async Task<StudyGroup?> GetAsync(int groupId) =>
        await _db.StudyGroups.AsNoTracking().FirstOrDefaultAsync(group => group.Id == groupId);

    public async Task<StudyGroup?> CreateAsync(string userId, string name, string? description, int? subjectId)
    {
        name = name.Trim();
        description = description?.Trim();
        if (name.Length is < 1 or > 200 || description?.Length > 500) return null;
        if (subjectId.HasValue && !await _db.Subjects.AnyAsync(subject => subject.Id == subjectId.Value))
            return null;

        var group = new StudyGroup
        {
            Name = name,
            Description = description,
            SubjectId = subjectId,
            CreatedByUserId = userId,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        group.Members.Add(new StudyGroupMember
        {
            UserId = userId,
            JoinedAt = group.CreatedAt
        });
        _db.StudyGroups.Add(group);
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task<bool> JoinAsync(int groupId, string userId)
    {
        if (!await _db.StudyGroups.AnyAsync(group => group.Id == groupId)) return false;
        if (await IsMemberAsync(groupId, userId)) return true;
        _db.StudyGroupMembers.Add(new StudyGroupMember
        {
            GroupId = groupId,
            UserId = userId,
            JoinedAt = _clock.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ChatMessage>?> MessagesAsync(int groupId, string userId,
        int? beforeId = null, int limit = 30)
    {
        if (!await IsMemberAsync(groupId, userId)) return null;
        limit = Math.Clamp(limit, 1, 50);
        var query = _db.GroupMessages.AsNoTracking().Where(message => message.GroupId == groupId);
        if (beforeId.HasValue) query = query.Where(message => message.Id < beforeId.Value);
        var messages = await query.OrderByDescending(message => message.Id)
            .Take(limit)
            .Select(message => new ChatMessage(message.Id, message.GroupId,
                message.SenderUserId, message.SenderUser.Username,
                message.SenderUser.AvatarUrl, message.Content, message.SentAt))
            .ToListAsync();
        messages.Reverse();
        return messages;
    }

    public async Task<ChatMessage?> SendAsync(int groupId, string userId, string content)
    {
        content = content.Trim();
        if (content.Length is < 1 or > 2000 || !await IsMemberAsync(groupId, userId)) return null;
        var message = new GroupMessage
        {
            GroupId = groupId,
            SenderUserId = userId,
            Content = content,
            SentAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.GroupMessages.Add(message);
        await _db.SaveChangesAsync();
        var sender = await _db.Users.AsNoTracking().FirstAsync(user => user.Id == userId);
        return new ChatMessage(message.Id, groupId, userId, sender.Username,
            sender.AvatarUrl, content, message.SentAt);
    }

    public async Task<bool> MarkReadAsync(int groupId, string userId, int lastMessageId)
    {
        var member = await _db.StudyGroupMembers.FindAsync(groupId, userId);
        if (member is null) return false;
        if (!await _db.GroupMessages.AnyAsync(message => message.GroupId == groupId && message.Id == lastMessageId))
            return false;
        if (member.LastReadMessageId is null || lastMessageId > member.LastReadMessageId)
        {
            member.LastReadMessageId = lastMessageId;
            await _db.SaveChangesAsync();
        }
        return true;
    }
}
