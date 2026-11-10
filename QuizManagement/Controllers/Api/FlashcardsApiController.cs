using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

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
    /// Lấy danh sách thẻ flashcard để ôn tập trong một bộ đề.
    /// Tham số query mode: "due" (mặc định - các thẻ đến hạn) hoặc "all" (toàn bộ thẻ trong bộ đề).
    /// </summary>
    [HttpGet("api/decks/{deckId:int}/flashcards")]
    [ProducesResponseType<ApiFlashcardSession>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<ApiFlashcardSession> GetSession(int deckId, [FromQuery] string? mode = "due")
    {
        var includeAll = string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase);
        var session = _flashcardService.GetStudySession(CurrentUserId(), deckId, includeAll);
        if (session is null)
        {
            return NotFound(new ApiError("deck_not_found", "Không tìm thấy bộ đề."));
        }

        return ApiFlashcardSession.From(session);
    }

    /// <summary>
    /// Ghi nhận kết quả ôn tập một thẻ flashcard theo thuật toán lặp lại ngắt quãng (Spaced Repetition / Leitner).
    /// </summary>
    [HttpPost("api/flashcards/progress")]
    [ProducesResponseType<ApiFlashcardReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public ActionResult<ApiFlashcardReviewResponse> Review([FromBody] ApiFlashcardReviewRequest request)
    {
        var result = _flashcardService.Review(CurrentUserId(), request.DeckId, request.QuestionId, request.Remembered, request.ReviewId);
        if (result is null)
        {
            return NotFound(new ApiError("question_not_found", "Không tìm thấy câu hỏi trong bộ đề này."));
        }

        return ApiFlashcardReviewResponse.From(result);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Không tìm thấy UserId trong token.");
}
