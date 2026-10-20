using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Hubs;
using QuizManagement.Infrastructure;
using QuizManagement.Services;
using System.Security.Claims;
using BusinessObjects;

namespace QuizManagement.Controllers;

[ApiController]
[Route("api/groups")]
[ApiAuthorize]
public sealed class StudyGroupsApiController : ControllerBase
{
    private readonly StudyGroupService _groups;
    private readonly IHubContext<ChatHub> _hub;

    public StudyGroupsApiController(StudyGroupService groups, IHubContext<ChatHub> hub)
    {
        _groups = groups;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> Mine() => Ok(await _groups.ListAsync(UserId()));

    [HttpGet("discover")]
    public async Task<IActionResult> Discover() => Ok(await _groups.ListAsync(UserId(), discover: true));

    [HttpGet("discovery")]
    public async Task<IActionResult> Discovery([FromQuery] string? search, [FromQuery] StudyGroupAccessMode? accessMode,
        [FromQuery] int? subjectId, [FromQuery] string? sort, [FromQuery] int page = 1) =>
        Ok(await _groups.DiscoverPageAsync(UserId(), search, accessMode, subjectId, sort, page));

    [HttpGet("options")]
    public async Task<IActionResult> Options() => Ok(new
    {
        publicSubjects = await _groups.PublicSubjectsAsync(UserId()),
        mySubjects = await _groups.MySubjectsAsync(UserId())
    });

    [HttpGet("invitations")]
    public async Task<IActionResult> Invitations() => Ok(await _groups.InvitationsAsync(UserId()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudyGroupRequest request)
    {
        var group = await _groups.CreateAsync(UserId(), request.Name ?? "", request.Description,
            request.SubjectId, request.AccessMode);
        if (group is null)
            return BadRequest(new ApiError("invalid_group", "Tên, mô tả hoặc môn học không hợp lệ."));
        await NotifyMembershipEventAsync(group.Id, UserId());
        return Created($"/api/groups/{group.Id}", new { group.Id, group.Name });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        if (!await _groups.IsMemberAsync(id, UserId()))
            return NotFound(new ApiError("group_not_found", "Không tìm thấy nhóm."));
        var group = await _groups.GetAsync(id);
        var members = await _groups.MembersAsync(id, UserId());
        return group is null ? NotFound(new ApiError("group_not_found", "Không tìm thấy nhóm.")) : Ok(new
        {
            group.Id, group.Name, group.Description, group.SubjectId, group.CreatedAt, group.AccessMode,
            myRole = members?.FirstOrDefault(member => member.UserId == UserId())?.Role,
            members
        });
    }

    [HttpGet("{id:int}/management")]
    public async Task<IActionResult> Management(int id)
    {
        if (!await _groups.IsManagerAsync(id, UserId()))
            return StatusCode(403, new ApiError("group_forbidden", "Bạn không có quyền quản lý nhóm."));
        SensitiveResponse();
        return Ok(new
        {
            requests = await _groups.PendingRequestsAsync(id, UserId()),
            banned = await _groups.BannedUsersAsync(id, UserId()),
            inviteLinks = await _groups.InviteLinksAsync(id, UserId())
        });
    }

    [HttpPost("{id:int}/invitations")]
    public async Task<IActionResult> Invite(int id, [FromBody] InviteGroupMemberRequest request) =>
        ActionResult(await _groups.InviteAsync(id, UserId(), request.Email ?? ""));

    [HttpPost("{id:int}/invitation-response")]
    public async Task<IActionResult> RespondInvitation(int id, [FromBody] GroupDecisionRequest request)
    {
        var wasMember = await _groups.IsMemberAsync(id, UserId());
        var result = await _groups.RespondInvitationAsync(id, UserId(), request.Accept);
        if (result == GroupActionResult.Success && request.Accept && !wasMember)
            await NotifyJoinedAsync(id, UserId());
        return ActionResult(result);
    }

    [HttpPost("{id:int}/requests/{userId}/review")]
    public async Task<IActionResult> ReviewRequest(int id, string userId, [FromBody] GroupDecisionRequest request)
    {
        var wasMember = await _groups.IsMemberAsync(id, userId);
        var result = await _groups.ReviewRequestAsync(id, UserId(), userId, request.Accept);
        if (result == GroupActionResult.Success && request.Accept && !wasMember)
            await NotifyJoinedAsync(id, userId);
        return ActionResult(result);
    }

    [HttpPut("{id:int}/access")]
    public async Task<IActionResult> ChangeAccess(int id, [FromBody] GroupAccessRequest request) =>
        ActionResult(await _groups.ChangeAccessModeAsync(id, UserId(), request.AccessMode));

    [HttpPut("{id:int}/members/{userId}/moderator")]
    public async Task<IActionResult> SetModerator(int id, string userId, [FromBody] GroupModeratorRequest request)
    {
        var result = await _groups.SetModeratorAsync(id, UserId(), userId, request.Moderator);
        if (result == GroupActionResult.Success) await NotifyRosterAsync(id);
        return ActionResult(result);
    }

    [HttpPost("{id:int}/ownership")]
    public async Task<IActionResult> TransferOwnership(int id, [FromBody] GroupTargetRequest request)
    {
        var result = await _groups.TransferOwnershipAsync(id, UserId(), request.UserId ?? "");
        if (result == GroupActionResult.Success) await NotifyRosterAsync(id);
        return ActionResult(result);
    }

    [HttpDelete("{id:int}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, string userId)
    {
        var result = await _groups.RemoveMemberAsync(id, UserId(), userId);
        if (result == GroupActionResult.Success) await NotifyLeftAsync(id, userId);
        return ActionResult(result);
    }

    [HttpDelete("{id:int}/bans/{userId}")]
    public async Task<IActionResult> Unban(int id, string userId) =>
        ActionResult(await _groups.UnbanAsync(id, UserId(), userId));

    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id)
    {
        var result = await _groups.LeaveAsync(id, UserId());
        if (result == GroupActionResult.Success) await NotifyLeftAsync(id, UserId());
        return ActionResult(result);
    }

    [HttpPost("{id:int}/invite-links")]
    public async Task<IActionResult> CreateInviteLink(int id, [FromBody] GroupInviteLinkRequest request)
    {
        SensitiveResponse();
        if (!await _groups.IsOwnerAsync(id, UserId()))
            return StatusCode(403, new ApiError("group_forbidden", "Chỉ chủ phòng được quản lý link mời."));
        var token = await _groups.CreateInviteLinkAsync(id, UserId(), request.DurationDays);
        return token is null
            ? BadRequest(new ApiError("invalid_duration", "Thời hạn phải là 1, 7, 30 ngày hoặc 0 (vĩnh viễn)."))
            : Ok(new { token });
    }

    [HttpDelete("{id:int}/invite-links/{linkId:int}")]
    public async Task<IActionResult> RevokeInviteLink(int id, int linkId) =>
        ActionResult(await _groups.RevokeInviteLinkAsync(id, linkId, UserId()));

    [HttpGet("invite/{token}")]
    public async Task<IActionResult> PreviewInviteLink(string token)
    {
        SensitiveResponse();
        var preview = await _groups.InviteLinkPreviewAsync(token);
        return preview is null ? await UnavailableInviteLinkAsync(token) : Ok(preview);
    }

    [HttpPost("invite/{token}")]
    public async Task<IActionResult> AcceptInviteLink(string token)
    {
        SensitiveResponse();
        var preview = await _groups.InviteLinkPreviewAsync(token);
        if (preview is null) return await UnavailableInviteLinkAsync(token);
        var result = await _groups.JoinWithInviteLinkAsync(token, UserId());
        if (result == GroupJoinResult.Joined) await NotifyJoinedAsync(preview.GroupId, UserId());
        return result switch
        {
            GroupJoinResult.Joined or GroupJoinResult.AlreadyMember => Ok(new { id = preview.GroupId }),
            GroupJoinResult.Banned => StatusCode(403, new ApiError("group_banned", "Bạn đã bị chặn khỏi nhóm.")),
            _ => await UnavailableInviteLinkAsync(token)
        };
    }

    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id)
    {
        var result = await _groups.JoinAsync(id, UserId());
        if (result == GroupJoinResult.Joined)
        {
            await NotifyMembershipEventAsync(id, UserId());
            await _hub.Clients.Users(await _groups.RecipientIdsAsync(id))
                .SendAsync("RoomRosterChanged", id);
        }
        return result switch
        {
            GroupJoinResult.Joined or GroupJoinResult.AlreadyMember => NoContent(),
            GroupJoinResult.PendingApproval => Accepted(),
            GroupJoinResult.Banned => StatusCode(403, new ApiError("group_banned", "Bạn đã bị chặn khỏi nhóm.")),
            _ => NotFound(new ApiError("group_not_found", "Không tìm thấy nhóm."))
        };
    }

    [HttpDelete("{id:int}/join-request")]
    public async Task<IActionResult> CancelJoinRequest(int id) =>
        await _groups.CancelRequestAsync(id, UserId()) == GroupActionResult.Success
            ? NoContent() : NotFound(new ApiError("request_not_found", "Không tìm thấy yêu cầu tham gia."));

    [HttpGet("{id:int}/messages")]
    public async Task<IActionResult> Messages(int id, [FromQuery] int? beforeId, [FromQuery] int limit = 30)
    {
        var messages = await _groups.MessagesAsync(id, UserId(), beforeId, limit);
        return messages is null
            ? NotFound(new ApiError("group_not_found", "Không tìm thấy nhóm."))
            : Ok(messages);
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, [FromBody] MarkGroupReadRequest request) =>
        await _groups.MarkReadAsync(id, UserId(), request.LastMessageId)
            ? NoContent()
            : BadRequest(new ApiError("invalid_read_position", "Tin nhắn hoặc nhóm không hợp lệ."));

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IActionResult ActionResult(GroupActionResult result) => result switch
    {
        GroupActionResult.Success => NoContent(),
        GroupActionResult.Forbidden => StatusCode(403, new ApiError("group_forbidden", "Bạn không có quyền thực hiện thao tác này.")),
        GroupActionResult.Conflict => Conflict(new ApiError("group_conflict", "Tài khoản này đã là thành viên của nhóm.")),
        GroupActionResult.Invalid => BadRequest(new ApiError("invalid_group_action", "Thông tin không hợp lệ.")),
        _ => NotFound(new ApiError("group_action_not_found", "Không tìm thấy nhóm, tài khoản hoặc yêu cầu hợp lệ."))
    };

    private void SensitiveResponse()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private async Task<IActionResult> UnavailableInviteLinkAsync(string token) =>
        await _groups.InviteLinkFailureAsync(token) switch
        {
            GroupInviteLinkFailure.Expired => StatusCode(410, new ApiError("invite_expired", "Link mời đã hết hạn.")),
            GroupInviteLinkFailure.Revoked => StatusCode(410, new ApiError("invite_revoked", "Link mời đã bị thu hồi.")),
            _ => NotFound(new ApiError("invite_not_found", "Không tìm thấy link mời."))
        };

    private async Task NotifyRosterAsync(int id) =>
        await _hub.Clients.Users(await _groups.RecipientIdsAsync(id)).SendAsync("RoomRosterChanged", id);

    private async Task NotifyJoinedAsync(int id, string userId)
    {
        await NotifyMembershipEventAsync(id, userId);
        await NotifyRosterAsync(id);
    }

    private async Task NotifyLeftAsync(int id, string userId)
    {
        await _hub.Clients.User(userId).SendAsync("GroupAccessRevoked", id);
        var message = await _groups.RecordMembershipEventAsync(id, userId, GroupMessageKind.Left);
        await _hub.Clients.Users(await _groups.RecipientIdsAsync(id)).SendAsync("ReceiveMessage", message);
        await NotifyRosterAsync(id);
    }

    private async Task NotifyMembershipEventAsync(int groupId, string userId)
    {
        var message = await _groups.RecordMembershipEventAsync(groupId, userId, GroupMessageKind.Joined);
        await _hub.Clients.Users(await _groups.RecipientIdsAsync(groupId))
            .SendAsync("ReceiveMessage", message);
    }
}

public sealed record CreateStudyGroupRequest(string? Name, string? Description, int? SubjectId,
    StudyGroupAccessMode AccessMode = StudyGroupAccessMode.PublicOpen);
public sealed record MarkGroupReadRequest(int LastMessageId);
public sealed record InviteGroupMemberRequest(string? Email);
public sealed record GroupDecisionRequest(bool Accept);
public sealed record GroupAccessRequest(StudyGroupAccessMode AccessMode);
public sealed record GroupModeratorRequest(bool Moderator);
public sealed record GroupTargetRequest(string? UserId);
public sealed record GroupInviteLinkRequest(int DurationDays);
