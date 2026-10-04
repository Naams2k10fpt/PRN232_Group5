using BusinessObjects;
using DataAccessObjects;

namespace Repositories
{
    public class CatalogRepository : ICatalogRepository
    {
        private readonly QuizManagementDbContext _context;

        public CatalogRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public PagedResult<SubjectSummaryReadModel> GetSubjectSummaries(string? search, int page, int pageSize)
            => CatalogDAO.Instance.GetSubjectSummaries(_context, search, page, pageSize);

        public SubjectSummaryReadModel? GetSubjectSummary(int id)
            => CatalogDAO.Instance.GetSubjectSummary(_context, id);

        public bool SubjectExists(int id)
            => CatalogDAO.Instance.SubjectExists(_context, id);

        public List<DeckSummaryReadModel> GetDeckSummaries(int subjectId, string userId, DateTime nowUtc)
            => CatalogDAO.Instance.GetDeckSummaries(_context, subjectId, userId, nowUtc);

        public DeckSummaryReadModel? GetDeckSummary(int deckId, string userId, DateTime nowUtc)
            => CatalogDAO.Instance.GetDeckSummary(_context, deckId, userId, nowUtc);

        public List<Question> GetFlashcardsByDeck(int deckId)
            => CatalogDAO.Instance.GetFlashcardsByDeck(_context, deckId);

        public bool QuestionBelongsToDeck(int deckId, int questionId)
            => CatalogDAO.Instance.QuestionBelongsToDeck(_context, deckId, questionId);
    }
}
