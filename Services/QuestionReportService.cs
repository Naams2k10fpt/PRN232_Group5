using BusinessObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories;

namespace Services;

public class QuestionReportService : IQuestionReportService
{
    private readonly IQuestionReportRepository _repository;

    public QuestionReportService(IQuestionReportRepository repository)
    {
        _repository = repository;
    }

    public QuestionReportSubmission Submit(int questionId, string userId, string reason, string? note)
    {
        if (_repository.HasPendingReport(questionId, userId))
            return QuestionReportSubmission.AlreadyPending;

        var report = new QuestionReport
        {
            QuestionId = questionId,
            UserId = userId,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };
        try
        {
            _repository.Create(report);
            return QuestionReportSubmission.Submitted;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return QuestionReportSubmission.AlreadyPending;
        }
    }

    public List<QuestionReport> GetAllReports() => _repository.GetAll();
    public List<QuestionReport> GetReportsByContentOwner(string ownerUserId) => _repository.GetByContentOwner(ownerUserId);
    public Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
        string ownerUserId,
        bool allowAll,
        int page,
        int pageSize)
        => _repository.GetPageAsync(ownerUserId, allowAll, page, pageSize);
    public QuestionReportResolution Resolve(int reportId, string actorUserId, bool allowAll)
    {
        var report = _repository.GetForResolution(reportId, actorUserId, allowAll);
        if (report is null) return QuestionReportResolution.NotFound;
        if (report.IsResolved) return QuestionReportResolution.AlreadyResolved;

        _repository.Resolve(report);
        return QuestionReportResolution.Resolved;
    }

}
