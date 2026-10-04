using Microsoft.AspNetCore.Mvc;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

// Subjects for the mobile home screen. Every signed-in role can browse them, like the web Subjects page.
[ApiController]
[Route("api/subjects")]
[ApiAuthorize(Policy = "StudyContent")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public class SubjectsApiController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public SubjectsApiController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>
    /// Danh sách môn học kèm số bộ đề và số câu hỏi. `search` tìm theo tên (không phân biệt hoa thường);
    /// `pageSize` tối đa 100.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiPage<ApiSubject>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public ActionResult<ApiPage<ApiSubject>> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CatalogService.DefaultPageSize)
    {
        var result = _catalogService.GetSubjects(search, page, pageSize);
        return new ApiPage<ApiSubject>(
            result.Items.Select(ApiSubject.From).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    /// <summary>Chi tiết một môn học.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiSubject>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<ApiSubject> Get(int id)
    {
        var subject = _catalogService.GetSubject(id);
        if (subject is null)
            return NotFound(new ApiError("subject_not_found", "Không tìm thấy môn học."));

        return ApiSubject.From(subject);
    }
}
