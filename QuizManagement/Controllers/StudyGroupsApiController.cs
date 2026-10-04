using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Services;
using System.Security.Claims;
using BusinessObjects;

namespace QuizManagement.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public sealed class StudyGroupsApiController : ControllerBase
{
    private readonly StudyGroupService _groups;

    public StudyGroupsApiController(StudyGroupService groups) => _groups = groups;

    [HttpGet]
    public async Task<IActionResult> Mine() => Ok(await _groups.ListAsync(UserId()));

    [HttpGet("discover")]
    public async Task<IActionResult> Discover() => Ok(await _groups.ListAsync(UserId(), discover: true));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudyGroupRequest request)
    {
        var group = await _groups.CreateAsync(UserId(), request.Name ?? "", request.Description,
            request.SubjectId, request.AccessMode);
        return group is null
            ? BadRequest(new { message = "Tên, mô tả hoặc môn học không hợp lệ." })
            : Created($"/api/groups/{group.Id}", new { group.Id, group.Name });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        if (!await _groups.IsMemberAsync(id, UserId())) return NotFound();
        var group = await _groups.GetAsync(id);
        return group is null ? NotFound() : Ok(new
        {
            group.Id, group.Name, group.Description, group.SubjectId, group.CreatedAt, group.AccessMode
        });
    }

    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id)
    {
        var result = await _groups.JoinAsync(id, UserId());
        return result switch
        {
            GroupJoinResult.Joined or GroupJoinResult.AlreadyMember => NoContent(),
            GroupJoinResult.PendingApproval => Accepted(),
            GroupJoinResult.Banned => Forbid(),
            _ => NotFound()
        };
    }

    [HttpDelete("{id:int}/join-request")]
    public async Task<IActionResult> CancelJoinRequest(int id) =>
        await _groups.CancelRequestAsync(id, UserId()) == GroupActionResult.Success
            ? NoContent() : NotFound();

    [HttpGet("{id:int}/messages")]
    public async Task<IActionResult> Messages(int id, [FromQuery] int? beforeId, [FromQuery] int limit = 30)
    {
        var messages = await _groups.MessagesAsync(id, UserId(), beforeId, limit);
        return messages is null ? NotFound() : Ok(messages);
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, [FromBody] MarkGroupReadRequest request) =>
        await _groups.MarkReadAsync(id, UserId(), request.LastMessageId)
            ? NoContent()
            : BadRequest(new { message = "Tin nhắn hoặc nhóm không hợp lệ." });

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

public sealed record CreateStudyGroupRequest(string? Name, string? Description, int? SubjectId,
    StudyGroupAccessMode AccessMode = StudyGroupAccessMode.PublicOpen);
public sealed record MarkGroupReadRequest(int LastMessageId);
