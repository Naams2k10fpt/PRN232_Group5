using BusinessObjects;
using DataAccessObjects;

namespace Repositories;

public class QuestionReportRepository : IQuestionReportRepository
{
    private readonly QuizManagementDbContext _context;

    public QuestionReportRepository(QuizManagementDbContext context)
    {
        _context = context;
    }

    public void Create(QuestionReport report) => QuestionReportDAO.Instance.Create(_context, report);
    public List<QuestionReport> GetAll() => QuestionReportDAO.Instance.GetAll(_context);
    public List<QuestionReport> GetByContentOwner(string ownerUserId) => QuestionReportDAO.Instance.GetByContentOwner(_context, ownerUserId);
    public Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
        string ownerUserId,
        bool allowAll,
        int page,
        int pageSize)
        => QuestionReportDAO.Instance.GetPageAsync(_context, ownerUserId, allowAll, page, pageSize);
    public QuestionReport? GetForResolution(int id, string actorUserId, bool allowAll)
        => QuestionReportDAO.Instance.GetForResolution(_context, id, actorUserId, allowAll);
    public void Resolve(QuestionReport report) => QuestionReportDAO.Instance.Resolve(_context, report);
    public bool HasPendingReport(int questionId, string userId) => QuestionReportDAO.Instance.HasPendingReport(_context, questionId, userId);
}
