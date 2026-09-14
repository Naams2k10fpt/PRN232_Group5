using BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects;

public class QuestionReportDAO
{
    private static QuestionReportDAO? _instance;
    private static readonly object _lock = new();

    private QuestionReportDAO() { }

    public static QuestionReportDAO Instance
    {
        get { lock (_lock) { return _instance ??= new QuestionReportDAO(); } }
    }

    public void Create(QuizManagementDbContext context, QuestionReport report)
    {
        context.QuestionReports.Add(report);
        context.SaveChanges();
    }

    // Returns reports with Question (ignores soft-delete filter) + User + Deck + Subject
    public List<QuestionReport> GetAll(QuizManagementDbContext context)
    {
        return context.QuestionReports
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .Include(r => r.Question.Deck.Subject)
            .AsNoTracking()
            .OrderBy(r => r.IsResolved)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
    }

    // Only reports where Question belongs to decks owned by ownerUserId
    public List<QuestionReport> GetByContentOwner(QuizManagementDbContext context, string ownerUserId)
    {
        return context.QuestionReports
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .Include(r => r.Question.Deck.Subject)
            .Where(r => r.Question.Deck.Subject.UserId == ownerUserId)
            .AsNoTracking()
            .OrderBy(r => r.IsResolved)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
        QuizManagementDbContext context,
        string ownerUserId,
        bool allowAll,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = context.QuestionReports
            .IgnoreQueryFilters()
            .AsQueryable();
        if (!allowAll)
        {
            query = query.Where(report => report.Question.Deck.Subject.UserId == ownerUserId);
        }

        var totalCount = await query.CountAsync();
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize)));
        var reports = await query
            .Include(report => report.User)
            .Include(report => report.Question.Deck.Subject)
            .AsNoTracking()
            .OrderBy(report => report.IsResolved)
            .ThenByDescending(report => report.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (reports, totalCount);
    }

    public QuestionReport? GetForResolution(
        QuizManagementDbContext context,
        int id,
        string actorUserId,
        bool allowAll)
        => context.QuestionReports
            .IgnoreQueryFilters()
            .FirstOrDefault(report =>
            report.Id == id &&
            (allowAll || report.Question.Deck.Subject.UserId == actorUserId));

    public void Resolve(QuizManagementDbContext context, QuestionReport report)
    {
        report.IsResolved = true;
        context.SaveChanges();
    }

    public bool HasPendingReport(QuizManagementDbContext context, int questionId, string userId)
        => context.QuestionReports.Any(r => r.QuestionId == questionId && r.UserId == userId && !r.IsResolved);
}
