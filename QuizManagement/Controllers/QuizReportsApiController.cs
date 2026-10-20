using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Infrastructure;
using QuizManagement.ViewModels.Quiz;
using Services;

namespace QuizManagement.Controllers;

[ApiController]
[Route("api/quiz/reports")]
[ApiAuthorize(Policy = "StudyContent")]
public sealed class QuizReportsApiController(IQuizService quiz, IQuestionService questions,
    IQuestionReportService reports) : ControllerBase
{
    [HttpPost]
    public IActionResult Create(QuizReportRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var history = quiz.GetTestHistoryById(request.TestHistoryId, userId);
        if (history is null || !QuizResultViewModel.FromHistory(history).Questions
                .Any(question => question.QuestionId == request.QuestionId)
            || questions.GetQuestionById(request.QuestionId, userId, allowAll: true) is null)
            return NotFound(new ApiError("question_not_found", "Không tìm thấy câu hỏi trong bài làm của bạn."));
        return reports.Submit(request.QuestionId, userId, request.Reason, request.Note)
            == QuestionReportSubmission.AlreadyPending
            ? Conflict(new ApiError("report_pending", "Bạn đã báo cáo câu hỏi này và đang chờ xử lý."))
            : NoContent();
    }
}

public sealed class QuizReportRequest
{
    [Range(1, int.MaxValue)] public int TestHistoryId { get; set; }
    [Range(1, int.MaxValue)] public int QuestionId { get; set; }
    [Required, RegularExpression("^(WrongAnswer|UnclearQuestion|DuplicateQuestion|Other)$")]
    public string Reason { get; set; } = "";
    [MaxLength(500)] public string? Note { get; set; }
}
