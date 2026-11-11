using BusinessObjects;

namespace Services
{
    public interface ICatalogService
    {
        // page and pageSize are clamped to valid values; the result reports the values actually used.
        PagedResult<SubjectSummaryReadModel> GetSubjects(string? search, int page, int pageSize);

        SubjectSummaryReadModel? GetSubject(int id);

        // Returns null when the subject does not exist. Due/learned counts are for the given user.
        IReadOnlyList<DeckSummaryReadModel>? GetDecks(int subjectId, string userId);

        DeckSummaryReadModel? GetDeck(int deckId, string userId);
        Task<int[]> GetFavoritesAsync(string userId, FavoriteKind kind, CancellationToken cancellationToken);
        Task<bool> SetFavoriteAsync(string userId, FavoriteKind kind, int id, bool favorite, CancellationToken cancellationToken);
        Task ImportFavoritesAsync(string userId, FavoriteKind kind, int[] ids, CancellationToken cancellationToken);
    }
}
