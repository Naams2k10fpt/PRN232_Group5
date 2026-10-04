using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Quiz;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers;

[ApiController]
[Route("api/history")]
[Authorize(Policy = "StudyContent")]
public sealed class HistoryApiController : ControllerBase
{
    private readonly IQuizService _quiz;

    public HistoryApiController(IQuizService quiz) => _quiz = quiz;

    [HttpGet("user")]
    public async Task<IActionResult> UserHistory([FromQuery] int page = 1)
    {
        var result = await _quiz.GetTestHistoryPageAsync(UserId(), page, 20);
        return Ok(new
        {
            result.Page,
            result.PageSize,
            result.TotalCount,
            items = result.Items.Select(history => new
            {
                history.Id,
                history.DeckId,
                deckName = history.Deck.Name,
                subjectName = history.Deck.Subject.Name,
                history.Score,
                history.Percentage,
                history.CreatedAt
            })
        });
    }

    [HttpGet("{id:int}")]
    public IActionResult Detail(int id)
    {
        var history = _quiz.GetTestHistoryById(id, UserId());
        return history is null ? NotFound() : Ok(QuizResultViewModel.FromHistory(history));
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
