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

        public Task<int[]> GetFavoritesAsync(string userId, FavoriteKind kind, CancellationToken cancellationToken)
            => CatalogFavoritesDAO.ListAsync(_context, userId, kind, cancellationToken);

        public async Task<bool> SetFavoriteAsync(
            string userId, FavoriteKind kind, int id, bool favorite, CancellationToken cancellationToken)
            => await CatalogFavoritesDAO.SaveAsync(_context, userId, kind, [id], favorite, import: false, cancellationToken) > 0;

        public Task ImportFavoritesAsync(string userId, FavoriteKind kind, int[] ids, CancellationToken cancellationToken)
            => CatalogFavoritesDAO.SaveAsync(_context, userId, kind, ids, favorite: true, import: true, cancellationToken);
    }
}
