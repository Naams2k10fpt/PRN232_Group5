using BusinessObjects;

namespace Repositories;

public interface IQuestionReportRepository
{
    void Create(QuestionReport report);
    List<QuestionReport> GetAll();
    List<QuestionReport> GetByContentOwner(string ownerUserId);
    Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
        string ownerUserId,
        bool allowAll,
        int page,
        int pageSize);
    QuestionReport? GetForResolution(int id, string actorUserId, bool allowAll);
    void Resolve(QuestionReport report);
    bool HasPendingReport(int questionId, string userId);
}
