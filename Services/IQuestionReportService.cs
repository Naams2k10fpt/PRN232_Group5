using BusinessObjects;

namespace Services;

public interface IQuestionReportService
{
    QuestionReportSubmission Submit(int questionId, string userId, string reason, string? note);
    List<QuestionReport> GetAllReports();
    List<QuestionReport> GetReportsByContentOwner(string ownerUserId);
    Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
        string ownerUserId,
        bool allowAll,
        int page,
        int pageSize);
    QuestionReportResolution Resolve(int reportId, string actorUserId, bool allowAll);
}

public enum QuestionReportResolution
{
    Resolved,
    AlreadyResolved,
    NotFound
}

public enum QuestionReportSubmission
{
    Submitted,
    AlreadyPending
}
