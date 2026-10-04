using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Quiz;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers;

[ApiController]
[Route("api/quiz")]
[Authorize(Policy = "StudyContent")]
public sealed class QuizApiController : ControllerBase
{
    private readonly IDeckService _decks;
    private readonly IQuizService _quiz;

    public QuizApiController(IDeckService decks, IQuizService quiz)
    {
        _decks = decks;
        _quiz = quiz;
    }

    [HttpPost("start")]
    public IActionResult Start([FromBody] StartQuizRequest request)
    {
        if (request.DeckId <= 0)
            return BadRequest(new { message = "Bộ đề không hợp lệ." });
        var deck = _decks.GetDeckForStudy(request.DeckId);
        if (deck is null) return NotFound(new { message = "Bộ đề không tồn tại." });

        var available = _quiz.GetAvailableQuestionCount(request.DeckId);
        var count = request.QuestionCount ?? Math.Min(10, available);
        if (count < 1 || count > available || deck.TimeLimitMinutes is < 0 or > 180)
            return BadRequest(new { message = "Số câu hỏi hoặc thời gian làm bài không hợp lệ." });

        var questions = _quiz.GetQuestionsForQuiz(request.DeckId, count);
        if (questions.Count != count)
            return Conflict(new { message = "Bộ đề vừa thay đổi. Vui lòng thử lại." });

        var attempt = _quiz.StartQuizAttempt(request.DeckId, UserId(),
            questions.Select(question => question.Id).ToList(), deck.TimeLimitMinutes);

        return Ok(new
        {
            attemptId = attempt.Id,
            deckId = deck.Id,
            deckName = deck.Name,
            startedAtUtc = attempt.StartedAtUtc,
            expiresAtUtc = attempt.ExpiresAtUtc,
            questions = ToQuestions(questions)
        });
    }

    [HttpGet("attempts/{attemptId:guid}")]
    public IActionResult GetAttempt(Guid attemptId, [FromQuery] int deckId)
    {
        if (attemptId == Guid.Empty || deckId <= 0)
            return BadRequest(new { message = "Lượt làm bài không hợp lệ." });
        var attempt = _quiz.GetValidQuizAttempt(attemptId, deckId, UserId());
        if (attempt is null) return NotFound(new { message = "Lượt làm bài không còn hiệu lực." });
        var deck = _decks.GetDeckForStudy(deckId);
        if (deck is null) return NotFound(new { message = "Bộ đề không tồn tại." });
        var questions = _quiz.GetQuestionsForAttempt(deckId, attempt.QuestionIds);
        if (questions.Count != attempt.QuestionIds.Count)
            return Conflict(new { message = "Câu hỏi của bộ đề đã thay đổi." });

        return Ok(new
        {
            attemptId,
            deckId,
            deckName = deck.Name,
            startedAtUtc = attempt.StartedAtUtc,
            expiresAtUtc = attempt.ExpiresAtUtc,
            questions = ToQuestions(questions)
        });
    }

    [HttpPost("submit")]
    public IActionResult Submit([FromBody] SubmitQuizRequest request)
    {
        if (request.AttemptId == Guid.Empty || request.DeckId <= 0
            || request.Answers is null || request.Answers.Count > 500
            || request.Answers.Any(answer => answer is null || answer.QuestionId <= 0
                || answer.SelectedAnswerIds is null || answer.SelectedAnswerIds.Count > 100
                || answer.SelectedAnswerIds.Any(id => id <= 0)))
            return BadRequest(new { message = "Lượt làm bài không hợp lệ." });

        var answers = request.Answers
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key,
                group => group.SelectMany(answer => answer.SelectedAnswerIds ?? []).Distinct().ToList());
        var history = _quiz.SubmitQuizAttempt(request.AttemptId, request.DeckId, UserId(), answers);
        if (history is null)
            return Conflict(new { message = "Lượt làm bài đã hết hạn hoặc không còn hiệu lực." });

        return Ok(QuizResultViewModel.FromHistory(
            _quiz.GetTestHistoryById(history.Id, UserId()) ?? history));
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static object ToQuestions(IEnumerable<BusinessObjects.Question> questions) =>
        questions.Select(question => new
        {
            questionId = question.Id,
            content = question.Content,
            questionType = question.QuestionType,
            answers = question.Answers.Select(answer => new
            {
                answerId = answer.Id,
                content = answer.Content
            })
        }).ToList();
}

public sealed record StartQuizRequest(int DeckId, int? QuestionCount);
public sealed record QuizAnswerRequest(int QuestionId, List<int>? SelectedAnswerIds);
public sealed record SubmitQuizRequest(Guid AttemptId, int DeckId, List<QuizAnswerRequest>? Answers);
