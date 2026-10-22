using BusinessObjects;
using DataAccessObjects;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace QuizManagement.Services;

public sealed record GroupSummary(int Id, string Name, string? Description, int? SubjectId,
    string? SubjectName, string? OwnerName, int MemberCount, int UnreadCount, bool IsMember,
    StudyGroupAccessMode AccessMode, bool HasPendingRequest);
public sealed record GroupSubjectOption(int Id, string Name);
public sealed record GroupDiscoverPage(List<GroupSummary> Groups, int TotalCount, int Page, int PageSize)
{
    public int PageCount => (int)Math.Ceiling((double)TotalCount / PageSize);
}
public sealed record ChatMessage(int Id, int GroupId, string SenderUserId, string SenderName,
    string? SenderAvatarUrl, string Content, DateTime SentAt, GroupMessageKind Kind,
    int ChannelId = 0, int Revision = 0, DateTime? EditedAt = null, DateTime? DeletedAt = null,
    ChatReply? Reply = null, List<ChatFile>? Attachments = null, List<ReactionSummary>? Reactions = null);
public sealed record GroupInvitationSummary(int GroupId, string GroupName, DateTime ExpiresAt);
public sealed record GroupInviteLinkPreview(int GroupId, string GroupName, DateTime? ExpiresAt);
public enum GroupInviteLinkFailure { Unknown, Expired, Revoked }
public sealed record GroupInviteLinkSummary(int Id, string CreatedByName, DateTime CreatedAt,
    DateTime? ExpiresAt, DateTime? RevokedAt, string? Token);
public sealed record GroupMemberSummary(string UserId, string Username, StudyGroupMemberRole Role);
public sealed record GroupJoinRequestSummary(string UserId, string Username, DateTime RequestedAt);
public sealed record GroupBanSummary(string UserId, string Username);

public enum GroupJoinResult { Joined, AlreadyMember, PendingApproval, InvitationRequired, Banned, NotFound }
public enum GroupActionResult { Success, NotFound, Forbidden, Invalid, Conflict }

public sealed class StudyGroupService
{
    private readonly QuizManagementDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IDataProtector _inviteLinkProtector;

    public StudyGroupService(QuizManagementDbContext db, TimeProvider clock,
        IDataProtectionProvider protectionProvider)
    {
        _db = db;
        _clock = clock;
        _inviteLinkProtector = protectionProvider.CreateProtector("QuizManagement.StudyGroupInviteLink.v1");
    }

    public Task<bool> IsMemberAsync(int groupId, string userId) =>
        _db.StudyGroupMembers.AnyAsync(member => member.GroupId == groupId
            && member.UserId == userId && !member.IsBanned
            && !member.User.IsDisabled && member.User.EmailConfirmed);

    public Task<bool> IsManagerAsync(int groupId, string userId) =>
        _db.StudyGroupMembers.AnyAsync(member => member.GroupId == groupId
            && member.UserId == userId && !member.IsBanned
            && !member.User.IsDisabled && member.User.EmailConfirmed
            && member.Role >= StudyGroupMemberRole.Moderator);

    public Task<bool> IsOwnerAsync(int groupId, string userId) =>
        _db.StudyGroupMembers.AnyAsync(member => member.GroupId == groupId
            && member.UserId == userId && !member.IsBanned
            && !member.User.IsDisabled && member.User.EmailConfirmed
            && member.Role == StudyGroupMemberRole.Owner);

    public async Task<List<GroupSummary>> ListAsync(string userId, bool discover = false)
    {
        var query = _db.StudyGroups.AsNoTracking();
        if (!discover)
            query = query.Where(group => group.Members.Any(member => member.UserId == userId && !member.IsBanned));
        else
            query = query.Where(group => group.AccessMode != StudyGroupAccessMode.PrivateInviteOnly
                || group.Members.Any(member => member.UserId == userId && !member.IsBanned));

        return await Summarize(query.OrderByDescending(group => group.CreatedAt), userId).ToListAsync();
    }

    public async Task<GroupDiscoverPage> DiscoverPageAsync(string userId, string? search,
        StudyGroupAccessMode? accessMode, int? subjectId, string? sort, int page, int pageSize = 12)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = PublicRoomsFor(userId);
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var term = search[..Math.Min(search.Length, 100)].ToLower();
            query = query.Where(group => group.Name.ToLower().Contains(term)
                || (group.Description != null && group.Description.ToLower().Contains(term)));
        }
        if (accessMode is StudyGroupAccessMode.PublicOpen or StudyGroupAccessMode.PublicApproval)
            query = query.Where(group => group.AccessMode == accessMode);
        if (subjectId.HasValue)
            query = query.Where(group => group.SubjectId == subjectId);

        var totalCount = await query.CountAsync();
        var pageCount = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        page = Math.Clamp(page, 1, pageCount);
        query = sort == "popular"
            ? query.OrderByDescending(group => group.Members.Count(member => !member.IsBanned))
                .ThenByDescending(group => group.CreatedAt).ThenByDescending(group => group.Id)
            : query.OrderByDescending(group => group.CreatedAt).ThenByDescending(group => group.Id);

        var groups = await Summarize(query.Skip((page - 1) * pageSize).Take(pageSize), userId)
            .ToListAsync();
        return new GroupDiscoverPage(groups, totalCount, page, pageSize);
    }

    public Task<List<GroupSubjectOption>> PublicSubjectsAsync(string userId) =>
        _db.Subjects.AsNoTracking()
            .Where(subject => !subject.IsDeleted
                && PublicRoomsFor(userId).Any(group => group.SubjectId == subject.Id))
            .OrderBy(subject => subject.Name)
            .Select(subject => new GroupSubjectOption(subject.Id, subject.Name)).ToListAsync();

    public Task<List<GroupSubjectOption>> MySubjectsAsync(string userId) =>
        _db.Subjects.AsNoTracking().Where(subject => subject.UserId == userId && !subject.IsDeleted)
            .OrderBy(subject => subject.Name)
            .Select(subject => new GroupSubjectOption(subject.Id, subject.Name)).ToListAsync();

    private IQueryable<StudyGroup> PublicRoomsFor(string userId) =>
        _db.StudyGroups.AsNoTracking()
            .Where(group => group.AccessMode != StudyGroupAccessMode.PrivateInviteOnly
                && !group.Members.Any(member => member.UserId == userId));

    private static IQueryable<GroupSummary> Summarize(IQueryable<StudyGroup> query, string userId) =>
        query.Select(group => new GroupSummary(
                group.Id, group.Name, group.Description, group.SubjectId,
                group.Subject == null ? null : group.Subject.Name,
                group.Members.Where(member => member.Role == StudyGroupMemberRole.Owner && !member.IsBanned)
                    .Select(member => member.User.Username).FirstOrDefault(),
                group.Members.Count(member => !member.IsBanned),
                group.Messages.Count(message =>
                    message.Kind == GroupMessageKind.Chat && message.DeletedAt == null && !message.Channel.IsArchived &&
                    group.Members.Any(member => member.UserId == userId && !member.IsBanned
                        && !message.Channel.Reads.Any(read => read.UserId == userId && read.LastReadMessageId >= message.Id)
                        && message.SenderUserId != userId)),
                group.Members.Any(member => member.UserId == userId && !member.IsBanned),
                group.AccessMode,
                group.JoinRequests.Any(request => request.UserId == userId
                    && request.Status == StudyGroupRequestStatus.Pending)));

    public Task<List<GroupInvitationSummary>> InvitationsAsync(string userId) =>
        _db.StudyGroupInvitations.AsNoTracking()
            .Where(invitation => invitation.UserId == userId
                && invitation.Status == StudyGroupInvitationStatus.Pending
                && invitation.ExpiresAt > _clock.GetUtcNow().UtcDateTime)
            .OrderByDescending(invitation => invitation.InvitedAt)
            .Select(invitation => new GroupInvitationSummary(
                invitation.GroupId, invitation.Group.Name, invitation.ExpiresAt))
            .ToListAsync();

    public async Task<StudyGroup?> GetAsync(int groupId) =>
        await _db.StudyGroups.AsNoTracking().FirstOrDefaultAsync(group => group.Id == groupId);

    public async Task<StudyGroup?> CreateAsync(string userId, string name, string? description,
        int? subjectId, StudyGroupAccessMode accessMode = StudyGroupAccessMode.PublicOpen)
    {
        name = name.Trim();
        description = description?.Trim();
        if (name.Length is < 1 or > 200 || description?.Length > 500
            || !Enum.IsDefined(accessMode)) return null;
        if (subjectId.HasValue && !await _db.Subjects.AnyAsync(subject => subject.Id == subjectId.Value
            && subject.UserId == userId && !subject.IsDeleted))
            return null;

        var group = new StudyGroup
        {
            Name = name,
            Description = description,
            SubjectId = subjectId,
            CreatedByUserId = userId,
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            AccessMode = accessMode
        };
        group.Members.Add(new StudyGroupMember
        {
            UserId = userId,
            JoinedAt = group.CreatedAt,
            Role = StudyGroupMemberRole.Owner
        });
        group.Channels.Add(new ChatChannel { Name = "chung", IsDefault = true, CreatedAt = group.CreatedAt });
        group.Channels.Add(new ChatChannel { Name = "phong-hoc", Kind = ChatChannelKind.Voice,
            Topic = "Cùng học qua mic và camera", IsDefault = true, CreatedAt = group.CreatedAt });
        _db.StudyGroups.Add(group);
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task<GroupJoinResult> JoinAsync(int groupId, string userId)
    {
        var group = await _db.StudyGroups.FindAsync(groupId);
        if (group is null) return GroupJoinResult.NotFound;
        var member = await FindMemberAsync(groupId, userId);
        if (member?.IsBanned == true) return GroupJoinResult.Banned;
        if (member is not null) return GroupJoinResult.AlreadyMember;
        if (group.AccessMode == StudyGroupAccessMode.PrivateInviteOnly)
            return GroupJoinResult.InvitationRequired;
        if (group.AccessMode == StudyGroupAccessMode.PublicApproval)
        {
            var request = await _db.StudyGroupJoinRequests.FindAsync(groupId, userId);
            if (request is null)
                _db.StudyGroupJoinRequests.Add(new StudyGroupJoinRequest
                {
                    GroupId = groupId, UserId = userId,
                    RequestedAt = _clock.GetUtcNow().UtcDateTime
                });
            else if (request.Status != StudyGroupRequestStatus.Pending)
            {
                request.Status = StudyGroupRequestStatus.Pending;
                request.RequestedAt = _clock.GetUtcNow().UtcDateTime;
                request.ReviewedAt = null;
                request.ReviewedByUserId = null;
            }
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException exception) when (DbExceptionClassifier.IsUniqueViolation(exception))
            {
                _db.ChangeTracker.Clear();
            }
            return GroupJoinResult.PendingApproval;
        }
        _db.StudyGroupMembers.Add(new StudyGroupMember
        {
            GroupId = groupId,
            UserId = userId,
            JoinedAt = _clock.GetUtcNow().UtcDateTime
        });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (DbExceptionClassifier.IsUniqueViolation(exception))
        {
            _db.ChangeTracker.Clear();
            return GroupJoinResult.AlreadyMember;
        }
        return GroupJoinResult.Joined;
    }

    public async Task<List<GroupMemberSummary>?> MembersAsync(int groupId, string actorId)
    {
        if (!await IsMemberAsync(groupId, actorId)) return null;
        return await _db.StudyGroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId && !member.IsBanned)
            .OrderByDescending(member => member.Role).ThenBy(member => member.User.Username)
            .Select(member => new GroupMemberSummary(member.UserId, member.User.Username, member.Role))
            .ToListAsync();
    }

    public async Task<GroupActionResult> CancelRequestAsync(int groupId, string userId)
    {
        var request = await _db.StudyGroupJoinRequests.FindAsync(groupId, userId);
        if (request?.Status != StudyGroupRequestStatus.Pending) return GroupActionResult.NotFound;
        request.Status = StudyGroupRequestStatus.Cancelled;
        request.ReviewedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<List<GroupJoinRequestSummary>?> PendingRequestsAsync(int groupId, string actorId)
    {
        if (!await IsManagerAsync(groupId, actorId)) return null;
        return await _db.StudyGroupJoinRequests.AsNoTracking()
            .Where(request => request.GroupId == groupId
                && request.Status == StudyGroupRequestStatus.Pending)
            .OrderBy(request => request.RequestedAt)
            .Select(request => new GroupJoinRequestSummary(
                request.UserId, request.User.Username, request.RequestedAt))
            .ToListAsync();
    }

    public async Task<List<GroupBanSummary>?> BannedUsersAsync(int groupId, string actorId)
    {
        if (!await IsManagerAsync(groupId, actorId)) return null;
        return await _db.StudyGroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId && member.IsBanned)
            .OrderBy(member => member.User.Username)
            .Select(member => new GroupBanSummary(member.UserId, member.User.Username))
            .ToListAsync();
    }

    public async Task<GroupActionResult> ReviewRequestAsync(int groupId, string actorId,
        string targetId, bool approve)
    {
        if (!await IsManagerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        var group = await _db.StudyGroups.FindAsync(groupId);
        if (group is null || group.AccessMode != StudyGroupAccessMode.PublicApproval)
            return GroupActionResult.NotFound;
        var request = await _db.StudyGroupJoinRequests.FindAsync(groupId, targetId);
        if (request?.Status != StudyGroupRequestStatus.Pending) return GroupActionResult.NotFound;
        var member = await FindMemberAsync(groupId, targetId);
        if (approve && member?.IsBanned == true) return GroupActionResult.Forbidden;
        if (approve && !await _db.Users.AnyAsync(user => user.Id == targetId
            && !user.IsDisabled && user.EmailConfirmed)) return GroupActionResult.Forbidden;
        if (approve && member is null)
            _db.StudyGroupMembers.Add(new StudyGroupMember
            {
                GroupId = groupId, UserId = targetId,
                JoinedAt = _clock.GetUtcNow().UtcDateTime
            });
        request.Status = approve ? StudyGroupRequestStatus.Approved : StudyGroupRequestStatus.Rejected;
        request.ReviewedAt = _clock.GetUtcNow().UtcDateTime;
        request.ReviewedByUserId = actorId;
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> InviteAsync(int groupId, string actorId, string email)
    {
        if (!await IsManagerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        if (string.IsNullOrWhiteSpace(email)) return GroupActionResult.Invalid;
        var normalized = TextNormalization.Normalize(email);
        var user = await _db.Users.FirstOrDefaultAsync(candidate =>
            EF.Property<string>(candidate, "NormalizedEmail") == normalized);
        if (user is null || user.IsDisabled || !user.EmailConfirmed)
            return GroupActionResult.NotFound;
        var member = await FindMemberAsync(groupId, user.Id);
        if (member?.IsBanned == true) return GroupActionResult.Forbidden;
        if (member is not null) return GroupActionResult.Conflict;
        var now = _clock.GetUtcNow().UtcDateTime;
        var invitation = await _db.StudyGroupInvitations.FindAsync(groupId, user.Id);
        if (invitation is null)
            _db.StudyGroupInvitations.Add(new StudyGroupInvitation
            {
                GroupId = groupId, UserId = user.Id, InvitedByUserId = actorId,
                InvitedAt = now, ExpiresAt = now.AddDays(7)
            });
        else
        {
            invitation.Status = StudyGroupInvitationStatus.Pending;
            invitation.InvitedByUserId = actorId;
            invitation.InvitedAt = now;
            invitation.ExpiresAt = now.AddDays(7);
            invitation.RespondedAt = null;
        }
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> RespondInvitationAsync(int groupId, string userId, bool accept)
    {
        var invitation = await _db.StudyGroupInvitations.FindAsync(groupId, userId);
        if (invitation?.Status != StudyGroupInvitationStatus.Pending
            || invitation.ExpiresAt <= _clock.GetUtcNow().UtcDateTime)
            return GroupActionResult.NotFound;
        var member = await FindMemberAsync(groupId, userId);
        if (member?.IsBanned == true) return GroupActionResult.Forbidden;
        if (accept && member is null)
            _db.StudyGroupMembers.Add(new StudyGroupMember
            {
                GroupId = groupId, UserId = userId,
                JoinedAt = _clock.GetUtcNow().UtcDateTime
            });
        invitation.Status = accept
            ? StudyGroupInvitationStatus.Accepted : StudyGroupInvitationStatus.Declined;
        invitation.RespondedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<string?> CreateInviteLinkAsync(int groupId, string actorId, int durationDays)
    {
        if (!await IsOwnerAsync(groupId, actorId) || durationDays is not (0 or 1 or 7 or 30))
            return null;
        var now = _clock.GetUtcNow().UtcDateTime;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _db.StudyGroupInviteLinks.Add(new StudyGroupInviteLink
        {
            GroupId = groupId,
            TokenHash = HashInviteToken(token),
            ProtectedToken = _inviteLinkProtector.Protect(token),
            CreatedByUserId = actorId,
            CreatedAt = now,
            ExpiresAt = durationDays == 0 ? null : now.AddDays(durationDays)
        });
        await _db.SaveChangesAsync();
        return token;
    }

    public async Task<List<GroupInviteLinkSummary>> InviteLinksAsync(int groupId, string actorId)
    {
        if (!await IsOwnerAsync(groupId, actorId)) return [];
        var now = _clock.GetUtcNow().UtcDateTime;
        var links = await _db.StudyGroupInviteLinks.AsNoTracking()
            .Where(link => link.GroupId == groupId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => new { link.Id, CreatedByName = link.CreatedByUser.Username,
                link.CreatedAt, link.ExpiresAt, link.RevokedAt, link.ProtectedToken })
            .ToListAsync();
        return links.Select(link => new GroupInviteLinkSummary(link.Id, link.CreatedByName,
            link.CreatedAt, link.ExpiresAt, link.RevokedAt,
            link.RevokedAt is null && (link.ExpiresAt is null || link.ExpiresAt > now)
                ? TryUnprotectInviteToken(link.ProtectedToken) : null)).ToList();
    }

    public async Task<GroupActionResult> RevokeInviteLinkAsync(int groupId, int linkId, string actorId)
    {
        if (!await IsOwnerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        var link = await _db.StudyGroupInviteLinks.FirstOrDefaultAsync(candidate =>
            candidate.Id == linkId && candidate.GroupId == groupId);
        if (link is null || link.RevokedAt is not null) return GroupActionResult.NotFound;
        link.RevokedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<GroupInviteLinkPreview?> InviteLinkPreviewAsync(string token)
    {
        var hash = TryHashInviteToken(token);
        if (hash is null) return null;
        var now = _clock.GetUtcNow().UtcDateTime;
        return await _db.StudyGroupInviteLinks.AsNoTracking()
            .Where(link => link.TokenHash == hash && link.RevokedAt == null
                && (link.ExpiresAt == null || link.ExpiresAt > now))
            .Select(link => new GroupInviteLinkPreview(link.GroupId, link.Group.Name, link.ExpiresAt))
            .FirstOrDefaultAsync();
    }

    public async Task<GroupInviteLinkFailure> InviteLinkFailureAsync(string token)
    {
        var hash = TryHashInviteToken(token);
        if (hash is null) return GroupInviteLinkFailure.Unknown;
        var link = await _db.StudyGroupInviteLinks.AsNoTracking()
            .Where(candidate => candidate.TokenHash == hash)
            .Select(candidate => new { candidate.ExpiresAt, candidate.RevokedAt })
            .FirstOrDefaultAsync();
        if (link is null) return GroupInviteLinkFailure.Unknown;
        if (link.RevokedAt is not null) return GroupInviteLinkFailure.Revoked;
        return link.ExpiresAt <= _clock.GetUtcNow().UtcDateTime
            ? GroupInviteLinkFailure.Expired : GroupInviteLinkFailure.Unknown;
    }

    public async Task<GroupJoinResult> JoinWithInviteLinkAsync(string token, string userId)
    {
        var hash = TryHashInviteToken(token);
        if (hash is null) return GroupJoinResult.NotFound;
        var now = _clock.GetUtcNow().UtcDateTime;
        var link = await _db.StudyGroupInviteLinks.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash
                && candidate.RevokedAt == null
                && (candidate.ExpiresAt == null || candidate.ExpiresAt > now));
        if (link is null) return GroupJoinResult.NotFound;
        if (!await _db.Users.AnyAsync(user => user.Id == userId && !user.IsDisabled && user.EmailConfirmed))
            return GroupJoinResult.NotFound;
        var member = await FindMemberAsync(link.GroupId, userId);
        if (member?.IsBanned == true) return GroupJoinResult.Banned;
        if (member is not null) return GroupJoinResult.AlreadyMember;
        _db.StudyGroupMembers.Add(new StudyGroupMember
        {
            GroupId = link.GroupId, UserId = userId, JoinedAt = now
        });
        var request = await _db.StudyGroupJoinRequests.FindAsync(link.GroupId, userId);
        if (request?.Status == StudyGroupRequestStatus.Pending)
            _db.StudyGroupJoinRequests.Remove(request);
        var invitation = await _db.StudyGroupInvitations.FindAsync(link.GroupId, userId);
        if (invitation?.Status == StudyGroupInvitationStatus.Pending)
        {
            invitation.Status = StudyGroupInvitationStatus.Accepted;
            invitation.RespondedAt = now;
        }
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (DbExceptionClassifier.IsUniqueViolation(exception))
        {
            _db.ChangeTracker.Clear();
            return GroupJoinResult.AlreadyMember;
        }
        return GroupJoinResult.Joined;
    }

    private static string HashInviteToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token)));

    private static string? TryHashInviteToken(string token)
    {
        if (token?.Length != 64) return null;
        try { return HashInviteToken(token); }
        catch (FormatException) { return null; }
    }

    private string? TryUnprotectInviteToken(string protectedToken)
    {
        try { return _inviteLinkProtector.Unprotect(protectedToken); }
        catch (CryptographicException) { return null; }
    }

    public async Task<GroupActionResult> ChangeAccessModeAsync(int groupId, string actorId,
        StudyGroupAccessMode mode)
    {
        if (!Enum.IsDefined(mode)) return GroupActionResult.Invalid;
        if (!await IsOwnerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        var group = await _db.StudyGroups.FindAsync(groupId);
        if (group is null) return GroupActionResult.NotFound;
        if (group.AccessMode == mode) return GroupActionResult.Success;
        group.AccessMode = mode;
        var now = _clock.GetUtcNow().UtcDateTime;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.StudyGroupJoinRequests
            .Where(request => request.GroupId == groupId
                && request.Status == StudyGroupRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(request => request.Status, StudyGroupRequestStatus.Rejected)
                .SetProperty(request => request.ReviewedAt, now)
                .SetProperty(request => request.ReviewedByUserId, actorId));
        await _db.StudyGroupInvitations
            .Where(invitation => invitation.GroupId == groupId
                && invitation.Status == StudyGroupInvitationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(invitation => invitation.Status, StudyGroupInvitationStatus.Revoked)
                .SetProperty(invitation => invitation.RespondedAt, now));
        await _db.StudyGroupInviteLinks
            .Where(link => link.GroupId == groupId && link.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(link => link.RevokedAt, now));
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        _db.ChangeTracker.Clear();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> SetModeratorAsync(int groupId, string actorId,
        string targetId, bool moderator)
    {
        if (!await IsOwnerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        var target = await FindMemberAsync(groupId, targetId);
        if (target is null || target.IsBanned) return GroupActionResult.NotFound;
        if (target.Role == StudyGroupMemberRole.Owner) return GroupActionResult.Forbidden;
        target.Role = moderator ? StudyGroupMemberRole.Moderator : StudyGroupMemberRole.Member;
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> TransferOwnershipAsync(int groupId, string actorId,
        string targetId)
    {
        if (!await IsOwnerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        var target = await _db.StudyGroupMembers.AsNoTracking()
            .FirstOrDefaultAsync(member => member.GroupId == groupId && member.UserId == targetId);
        if (target is null || target.IsBanned) return GroupActionResult.NotFound;
        if (targetId == actorId) return GroupActionResult.Success;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.StudyGroupMembers.Where(member => member.GroupId == groupId && member.UserId == actorId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(member => member.Role, StudyGroupMemberRole.Member));
        await _db.StudyGroupMembers.Where(member => member.GroupId == groupId && member.UserId == targetId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(member => member.Role, StudyGroupMemberRole.Owner));
        await transaction.CommitAsync();
        _db.ChangeTracker.Clear();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> RemoveMemberAsync(int groupId, string actorId, string targetId)
    {
        var actor = await _db.StudyGroupMembers.AsNoTracking()
            .FirstOrDefaultAsync(member => member.GroupId == groupId && member.UserId == actorId && !member.IsBanned);
        if (actor is null || actor.Role < StudyGroupMemberRole.Moderator) return GroupActionResult.Forbidden;
        var target = await FindMemberAsync(groupId, targetId);
        if (target is null || target.IsBanned) return GroupActionResult.NotFound;
        if (target.Role >= actor.Role) return GroupActionResult.Forbidden;
        target.IsBanned = true;
        target.Role = StudyGroupMemberRole.Member;
        target.LastReadMessageId = null;
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> UnbanAsync(int groupId, string actorId, string targetId)
    {
        if (!await IsManagerAsync(groupId, actorId)) return GroupActionResult.Forbidden;
        var target = await FindMemberAsync(groupId, targetId);
        if (target is null || !target.IsBanned) return GroupActionResult.NotFound;
        _db.StudyGroupMembers.Remove(target);
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public async Task<GroupActionResult> LeaveAsync(int groupId, string userId)
    {
        var member = await FindMemberAsync(groupId, userId);
        if (member is null || member.IsBanned) return GroupActionResult.NotFound;
        if (member.Role == StudyGroupMemberRole.Owner) return GroupActionResult.Forbidden;
        _db.StudyGroupMembers.Remove(member);
        await _db.SaveChangesAsync();
        return GroupActionResult.Success;
    }

    public Task<List<string>> RecipientIdsAsync(int groupId) =>
        _db.StudyGroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId && !member.IsBanned
                && !member.User.IsDisabled && member.User.EmailConfirmed)
            .Select(member => member.UserId).ToListAsync();

    public Task<List<int>> GroupIdsForUserAsync(string userId) =>
        _db.StudyGroupMembers.AsNoTracking()
            .Where(member => member.UserId == userId && !member.IsBanned
                && !member.User.IsDisabled && member.User.EmailConfirmed)
            .Select(member => member.GroupId).ToListAsync();

    public Task<List<string>> EligibleViewerIdsAsync(int groupId, string[] viewerIds) =>
        _db.StudyGroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId && viewerIds.Contains(member.UserId)
                && !member.IsBanned && !member.User.IsDisabled && member.User.EmailConfirmed)
            .Select(member => member.UserId).ToListAsync();

    public async Task<List<ChatMessage>?> MessagesAsync(int groupId, string userId,
        int? beforeId = null, int limit = 30)
    {
        if (!await IsMemberAsync(groupId, userId)) return null;
        limit = Math.Clamp(limit, 1, 50);
        var channelId = await DefaultChannelIdAsync(groupId);
        var query = _db.GroupMessages.AsNoTracking().Where(message => message.GroupId == groupId && message.ChannelId == channelId);
        if (beforeId.HasValue) query = query.Where(message => message.Id < beforeId.Value);
        var messages = await ChatService.Project(query.OrderByDescending(message => message.Id).Take(limit)).ToListAsync();
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
            ChannelId = await DefaultChannelIdAsync(groupId),
            SenderUserId = userId,
            Content = content,
            SentAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.GroupMessages.Add(message);
        await _db.SaveChangesAsync();
        var sender = await _db.Users.AsNoTracking().FirstAsync(user => user.Id == userId);
        return new ChatMessage(message.Id, groupId, userId, sender.Username,
            sender.AvatarUrl, content, message.SentAt, GroupMessageKind.Chat, message.ChannelId);
    }

    public async Task<ChatMessage> RecordMembershipEventAsync(int groupId, string userId,
        GroupMessageKind kind)
    {
        if (kind is not (GroupMessageKind.Joined or GroupMessageKind.Left))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var sender = await _db.Users.AsNoTracking().FirstAsync(user => user.Id == userId);
        var message = new GroupMessage
        {
            GroupId = groupId,
            ChannelId = await DefaultChannelIdAsync(groupId),
            SenderUserId = userId,
            Content = string.Empty,
            Kind = kind,
            SentAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.GroupMessages.Add(message);
        await _db.SaveChangesAsync();
        return new ChatMessage(message.Id, groupId, userId, sender.Username,
            sender.AvatarUrl, string.Empty, message.SentAt, kind, message.ChannelId);
    }

    private Task<StudyGroupMember?> FindMemberAsync(int groupId, string userId) =>
        _db.StudyGroupMembers.FirstOrDefaultAsync(member =>
            member.GroupId == groupId && member.UserId == userId);

    public Task<int> DefaultChannelIdAsync(int groupId) => _db.ChatChannels
        .Where(channel => channel.GroupId == groupId && channel.IsDefault && channel.Kind == ChatChannelKind.Text)
        .Select(channel => channel.Id).SingleAsync();

    public async Task<bool> MarkReadAsync(int groupId, string userId, int lastMessageId)
    {
        var member = await FindMemberAsync(groupId, userId);
        if (member is null || member.IsBanned) return false;
        if (!await _db.GroupMessages.AnyAsync(message => message.GroupId == groupId && message.Id == lastMessageId))
            return false;
        var channelId = await _db.GroupMessages.Where(message => message.Id == lastMessageId)
            .Select(message => message.ChannelId).SingleAsync();
        await ChatService.AdvanceReadAsync(_db, channelId, userId, lastMessageId);
        if (member.LastReadMessageId is null || lastMessageId > member.LastReadMessageId)
        {
            member.LastReadMessageId = lastMessageId;
            await _db.SaveChangesAsync();
        }
        return true;
    }
}
