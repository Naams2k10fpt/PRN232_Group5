using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Hubs;
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

    public async Task<IActionResult> Index()
    {
        ViewBag.Invitations = await _groups.InvitationsAsync(UserId());
        return View(await _groups.ListAsync(UserId(), discover: true));
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
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Room), new { id = group.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id)
    {
        var result = await _groups.JoinAsync(id, UserId());
        if (result is GroupJoinResult.Joined or GroupJoinResult.AlreadyMember)
            return RedirectToAction(nameof(Room), new { id });
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
        var result = await _groups.RespondInvitationAsync(id, UserId(), accept);
        if (result != GroupActionResult.Success) return NotFound();
        return accept ? RedirectToAction(nameof(Room), new { id }) : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Room(int id)
    {
        if (!await _groups.IsMemberAsync(id, UserId())) return NotFound();
        var group = await _groups.GetAsync(id);
        if (group is null) return NotFound();
        ViewBag.UserId = UserId();
        ViewBag.Messages = await _groups.MessagesAsync(id, UserId());
        ViewBag.Members = await _groups.MembersAsync(id, UserId());
        return View(group);
    }

    public async Task<IActionResult> Manage(int id)
    {
        if (!await _groups.IsManagerAsync(id, UserId())) return NotFound();
        var group = await _groups.GetAsync(id);
        if (group is null) return NotFound();
        ViewBag.Members = await _groups.MembersAsync(id, UserId());
        ViewBag.Requests = await _groups.PendingRequestsAsync(id, UserId());
        ViewBag.Banned = await _groups.BannedUsersAsync(id, UserId());
        ViewBag.IsOwner = await _groups.IsOwnerAsync(id, UserId());
        ViewBag.UserId = UserId();
        return View(group);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRequest(int id, string userId, bool approve)
    {
        var result = await _groups.ReviewRequestAsync(id, UserId(), userId, approve);
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
        return result == GroupActionResult.Success
            ? RedirectToAction(nameof(Manage), new { id }) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferOwnership(int id, string userId)
    {
        var result = await _groups.TransferOwnershipAsync(id, UserId(), userId);
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
        return RedirectToAction(nameof(Index));
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
