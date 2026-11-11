using BusinessObjects;

namespace Repositories
{
    public interface ICatalogRepository
    {
        PagedResult<SubjectSummaryReadModel> GetSubjectSummaries(string? search, int page, int pageSize);
        SubjectSummaryReadModel? GetSubjectSummary(int id);
        bool SubjectExists(int id);
        List<DeckSummaryReadModel> GetDeckSummaries(int subjectId, string userId, DateTime nowUtc);
        DeckSummaryReadModel? GetDeckSummary(int deckId, string userId, DateTime nowUtc);
        List<Question> GetFlashcardsByDeck(int deckId);
        bool QuestionBelongsToDeck(int deckId, int questionId);
        Task<int[]> GetFavoritesAsync(string userId, FavoriteKind kind, CancellationToken cancellationToken);
        Task<bool> SetFavoriteAsync(string userId, FavoriteKind kind, int id, bool favorite, CancellationToken cancellationToken);
        Task ImportFavoritesAsync(string userId, FavoriteKind kind, int[] ids, CancellationToken cancellationToken);
    }
}
