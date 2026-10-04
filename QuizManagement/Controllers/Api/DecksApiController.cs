using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

// Decks of a subject, with the current user's flashcard progress in each deck.
[ApiController]
[ApiAuthorize(Policy = "StudyContent")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public class DecksApiController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public DecksApiController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>
    /// Danh sách bộ đề của một môn học, kèm số câu hỏi và tiến độ flashcard của người dùng hiện tại
    /// (số thẻ đến hạn, số thẻ đã thuộc).
    /// </summary>
    [HttpGet("api/subjects/{subjectId:int}/decks")]
    [ProducesResponseType<List<ApiDeck>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<List<ApiDeck>> ListBySubject(int subjectId)
    {
        var decks = _catalogService.GetDecks(subjectId, CurrentUserId());
        if (decks is null)
            return NotFound(new ApiError("subject_not_found", "Không tìm thấy môn học."));

        return decks.Select(ApiDeck.From).ToList();
    }

    /// <summary>Chi tiết một bộ đề kèm tiến độ flashcard của người dùng hiện tại.</summary>
    [HttpGet("api/decks/{id:int}")]
    [ProducesResponseType<ApiDeck>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<ApiDeck> Get(int id)
    {
        var deck = _catalogService.GetDeck(id, CurrentUserId());
        if (deck is null)
            return NotFound(new ApiError("deck_not_found", "Không tìm thấy bộ đề."));

        return ApiDeck.From(deck);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Không tìm thấy UserId trong token.");
}
