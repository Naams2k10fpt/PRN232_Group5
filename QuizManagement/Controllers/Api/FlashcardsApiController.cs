using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

// Mobile flashcard study. Uses the same FlashcardService as the web FlashcardsController, so both
// clients share one schedule per user.
[ApiController]
[ApiAuthorize(Policy = "StudyContent")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public class FlashcardsApiController : ControllerBase
{
    private readonly IFlashcardService _flashcardService;

    public FlashcardsApiController(IFlashcardService flashcardService)
    {
        _flashcardService = flashcardService;
    }

    /// <summary>
    /// Danh sách flashcard của một bộ đề. `mode=due` (mặc định): thẻ mới và thẻ đã đến hạn ôn;
    /// `mode=all`: tất cả thẻ. `content` và `explanation` là Markdown.
    /// </summary>
    [HttpGet("api/decks/{deckId:int}/flashcards")]
    [ProducesResponseType<ApiFlashcardDeck>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<ApiFlashcardDeck> List(int deckId, [FromQuery] string mode = "due")
    {
        var includeAll = string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase);
        if (!includeAll && !string.Equals(mode, "due", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiError("validation_failed", "Dữ liệu không hợp lệ.",
                new Dictionary<string, string[]> { ["mode"] = ["mode chỉ nhận 'due' hoặc 'all'."] }));
        }

        var session = _flashcardService.GetStudySession(CurrentUserId(), deckId, includeAll);
        if (session is null)
            return NotFound(new ApiError("deck_not_found", "Không tìm thấy bộ đề."));

        return ApiFlashcardDeck.From(session);
    }

    /// <summary>
    /// Lưu kết quả ôn một thẻ (`remembered`: đã nhớ / chưa nhớ) và trả về lịch ôn tiếp theo của thẻ đó.
    /// </summary>
    [HttpPost("api/flashcards/progress")]
    [ProducesResponseType<ReviewFlashcardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<ReviewFlashcardResponse> Review(ReviewFlashcardRequest request)
    {
        var result = _flashcardService.Review(
            CurrentUserId(), request.DeckId, request.QuestionId, request.Remembered!.Value);
        if (result is null)
            return NotFound(new ApiError("question_not_found", "Câu hỏi không thuộc bộ đề này hoặc đã bị xóa."));

        return ReviewFlashcardResponse.From(result);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Không tìm thấy UserId trong token.");
}
