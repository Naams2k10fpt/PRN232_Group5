using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Hubs;
using QuizManagement.Models;
using QuizManagement.Services;
using System.Security.Claims;

namespace QuizManagement.Controllers;

[Authorize]
public sealed class StudyGroupsController : Controller
{
    private readonly StudyGroupService _groups;
    private readonly IHubContext<ChatHub> _hub;

    public StudyGroupsController(StudyGroupService groups, IHubContext<ChatHub> hub)
    {
        _groups = groups;
        _hub = hub;
    }

    public async Task<IActionResult> Index(string? search, StudyGroupAccessMode? accessMode,
        int? subjectId, string? sort, int page = 1)
    {
        var userId = UserId();
        search = search?.Trim();
        if (search?.Length > 100) search = search[..100];
        if (accessMode is not (StudyGroupAccessMode.PublicOpen or StudyGroupAccessMode.PublicApproval))
            accessMode = null;
        sort = sort == "popular" ? "popular" : "newest";
        return View(new StudyGroupsIndexViewModel
        {
            MyGroups = await _groups.ListAsync(userId),
            Discovery = await _groups.DiscoverPageAsync(userId, search, accessMode, subjectId, sort, page),
            Invitations = await _groups.InvitationsAsync(userId),
            PublicSubjects = await _groups.PublicSubjectsAsync(userId),
            MySubjects = await _groups.MySubjectsAsync(userId),
            Search = search ?? string.Empty,
            AccessMode = accessMode,
            SubjectId = subjectId,
            Sort = sort
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, int? subjectId,
        StudyGroupAccessMode accessMode)
    {
        var group = await _groups.CreateAsync(UserId(), name ?? "", description, subjectId, accessMode);
        if (group is null)
        {
            TempData["ErrorMessage"] = "Thông tin nhóm không hợp lệ.";
            TempData["OpenCreateGroup"] = true;
            TempData["CreateGroupName"] = name;
            TempData["CreateGroupDescription"] = description;
            TempData["CreateGroupSubjectId"] = subjectId;
            TempData["CreateGroupAccessMode"] = accessMode.ToString();
            return RedirectToAction(nameof(Index));
        }
        await NotifyMembershipEventAsync(group.Id, UserId(), GroupMessageKind.Joined);
        return RedirectToAction(nameof(Room), new { id = group.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id)
    {
        var result = await _groups.JoinAsync(id, UserId());
        if (result is GroupJoinResult.Joined or GroupJoinResult.AlreadyMember)
        {
            if (result == GroupJoinResult.Joined)
            {
                await NotifyMembershipEventAsync(id, UserId(), GroupMessageKind.Joined);
                await NotifyRoomRosterChangedAsync(id);
            }
            return RedirectToAction(nameof(Room), new { id });
        }
        if (result == GroupJoinResult.PendingApproval)
        {
            TempData["SuccessMessage"] = "Đã gửi yêu cầu tham gia. Hãy chờ người quản lý duyệt.";
            return RedirectToAction(nameof(Index));
        }
        return result == GroupJoinResult.Banned ? Forbid() : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRequest(int id)
    {
        await _groups.CancelRequestAsync(id, UserId());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondInvitation(int id, bool accept)
    {
        var wasMember = accept && await _groups.IsMemberAsync(id, UserId());
        var result = await _groups.RespondInvitationAsync(id, UserId(), accept);
        if (result != GroupActionResult.Success) return NotFound();
        if (accept && !wasMember)
        {
            await NotifyMembershipEventAsync(id, UserId(), GroupMessageKind.Joined);
            await NotifyRoomRosterChangedAsync(id);
        }
        return accept ? RedirectToAction(nameof(Room), new { id }) : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Room(int id)
    {
        if (!await _groups.IsMemberAsync(id, UserId())) return NotFound();
        var group = await _groups.GetAsync(id);
        if (group is null) return NotFound();
        ViewBag.UserId = UserId();
        ViewBag.Members = await _groups.MembersAsync(id, UserId());
        return View(group);
    }

    [HttpGet]
    public async Task<IActionResult> Messages(int id, int? beforeId, int limit = 30)
    {
        var messages = await _groups.MessagesAsync(id, UserId(), beforeId, limit);
        return messages is null ? NotFound() : Ok(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, int lastMessageId) =>
        await _groups.MarkReadAsync(id, UserId(), lastMessageId)
            ? NoContent()
            : BadRequest();

    public async Task<IActionResult> Manage(int id)
    {
        if (!await _groups.IsManagerAsync(id, UserId())) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var group = await _groups.GetAsync(id);
        if (group is null) return NotFound();
        ViewBag.Members = await _groups.MembersAsync(id, UserId());
        ViewBag.Requests = await _groups.PendingRequestsAsync(id, UserId());
        ViewBag.Banned = await _groups.BannedUsersAsync(id, UserId());
        ViewBag.IsOwner = await _groups.IsOwnerAsync(id, UserId());
        ViewBag.UserId = UserId();
        ViewBag.InviteLinks = await _groups.InviteLinksAsync(id, UserId());
        return View(group);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRequest(int id, string userId, bool approve)
    {
        var wasMember = approve && await _groups.IsMemberAsync(id, userId);
        var result = await _groups.ReviewRequestAsync(id, UserId(), userId, approve);
        if (result == GroupActionResult.Success && approve && !wasMember)
        {
            await NotifyMembershipEventAsync(id, userId, GroupMessageKind.Joined);
            await NotifyRoomRosterChangedAsync(id);
        }
        return result == GroupActionResult.Success
            ? RedirectToAction(nameof(Manage), new { id }) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(int id, string email)
    {
        var result = await _groups.InviteAsync(id, UserId(), email);
        TempData[result == GroupActionResult.Success ? "SuccessMessage" : "ErrorMessage"] =
            result == GroupActionResult.Success ? "Đã gửi lời mời trong ứng dụng."
            : "Không thể mời tài khoản này. Kiểm tra email và trạng thái thành viên.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInviteLink(int id, int durationDays)
    {
        var token = await _groups.CreateInviteLinkAsync(id, UserId(), durationDays);
        if (token is null) return NotFound();
        TempData["SuccessMessage"] = "Đã tạo link mời.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInviteLink(int id, int linkId)
    {
        var result = await _groups.RevokeInviteLinkAsync(id, linkId, UserId());
        if (result != GroupActionResult.Success) return NotFound();
        TempData["SuccessMessage"] = "Đã thu hồi link mời.";
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpGet("groups/invite/{token}")]
    public async Task<IActionResult> InviteLink(string token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var preview = await _groups.InviteLinkPreviewAsync(token);
        if (preview is null) return await UnavailableInviteLinkAsync(token);
        ViewBag.Token = token;
        ViewBag.IsMember = await _groups.IsMemberAsync(preview.GroupId, UserId());
        return View(preview);
    }

    [HttpPost("groups/invite/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInviteLink(string token)
    {
        var preview = await _groups.InviteLinkPreviewAsync(token);
        if (preview is null) return await UnavailableInviteLinkAsync(token);
        var result = await _groups.JoinWithInviteLinkAsync(token, UserId());
        if (result == GroupJoinResult.Joined)
        {
            await NotifyMembershipEventAsync(preview.GroupId, UserId(), GroupMessageKind.Joined);
            await NotifyRoomRosterChangedAsync(preview.GroupId);
        }
        if (result is GroupJoinResult.Joined or GroupJoinResult.AlreadyMember)
            return RedirectToAction(nameof(Room), new { id = preview.GroupId });
        return result == GroupJoinResult.Banned ? Forbid() : await UnavailableInviteLinkAsync(token);
    }

    private async Task<IActionResult> UnavailableInviteLinkAsync(string token)
    {
        var reason = await _groups.InviteLinkFailureAsync(token);
        if (reason == GroupInviteLinkFailure.Unknown) return NotFound();
        Response.StatusCode = StatusCodes.Status410Gone;
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return View("InviteLinkUnavailable", reason);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAccess(int id, StudyGroupAccessMode accessMode)
    {
        var result = await _groups.ChangeAccessModeAsync(id, UserId(), accessMode);
        return result == GroupActionResult.Success
            ? RedirectToAction(nameof(Manage), new { id }) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetModerator(int id, string userId, bool moderator)
    {
        var result = await _groups.SetModeratorAsync(id, UserId(), userId, moderator);
        if (result == GroupActionResult.Success) await NotifyRoomRosterChangedAsync(id);
        return result == GroupActionResult.Success
            ? RedirectToAction(nameof(Manage), new { id }) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferOwnership(int id, string userId)
    {
        var result = await _groups.TransferOwnershipAsync(id, UserId(), userId);
        if (result == GroupActionResult.Success) await NotifyRoomRosterChangedAsync(id);
        return result == GroupActionResult.Success
            ? RedirectToAction(nameof(Room), new { id }) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, string userId)
    {
        var result = await _groups.RemoveMemberAsync(id, UserId(), userId);
        if (result != GroupActionResult.Success) return NotFound();
        await _hub.Clients.User(userId).SendAsync("GroupAccessRevoked", id);
        await NotifyMembershipEventAsync(id, userId, GroupMessageKind.Left);
        await NotifyRoomRosterChangedAsync(id);
        return RedirectToAction(nameof(Manage), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(int id, string userId)
    {
        var result = await _groups.UnbanAsync(id, UserId(), userId);
        return result == GroupActionResult.Success
            ? RedirectToAction(nameof(Manage), new { id }) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id)
    {
        var result = await _groups.LeaveAsync(id, UserId());
        if (result != GroupActionResult.Success) return NotFound();
        await _hub.Clients.User(UserId()).SendAsync("GroupAccessRevoked", id);
        await NotifyMembershipEventAsync(id, UserId(), GroupMessageKind.Left);
        await NotifyRoomRosterChangedAsync(id);
        return RedirectToAction(nameof(Index));
    }

    private async Task NotifyMembershipEventAsync(int groupId, string userId, GroupMessageKind kind)
    {
        var message = await _groups.RecordMembershipEventAsync(groupId, userId, kind);
        var recipients = await _groups.RecipientIdsAsync(groupId);
        await _hub.Clients.Users(recipients).SendAsync("ReceiveMessage", message);
    }

    private async Task NotifyRoomRosterChangedAsync(int groupId)
    {
        var recipients = await _groups.RecipientIdsAsync(groupId);
        await _hub.Clients.Users(recipients).SendAsync("RoomRosterChanged", groupId);
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
