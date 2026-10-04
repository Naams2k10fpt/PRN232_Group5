using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Services;
using System.Security.Claims;

namespace QuizManagement.Controllers;

[Authorize]
public sealed class StudyGroupsController : Controller
{
    private readonly StudyGroupService _groups;

    public StudyGroupsController(StudyGroupService groups) => _groups = groups;

    public async Task<IActionResult> Index()
    {
        var groups = await _groups.ListAsync(UserId(), discover: true);
        return View(groups);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, int? subjectId)
    {
        var group = await _groups.CreateAsync(UserId(), name ?? "", description, subjectId);
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
        if (!await _groups.JoinAsync(id, UserId())) return NotFound();
        return RedirectToAction(nameof(Room), new { id });
    }

    public async Task<IActionResult> Room(int id)
    {
        if (!await _groups.IsMemberAsync(id, UserId())) return Forbid();
        var group = await _groups.GetAsync(id);
        if (group is null) return NotFound();
        ViewBag.UserId = UserId();
        ViewBag.Messages = await _groups.MessagesAsync(id, UserId());
        return View(group);
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
