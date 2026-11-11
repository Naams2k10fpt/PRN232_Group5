using BusinessObjects;
using Repositories;

namespace Services
{
    public class CatalogService : ICatalogService
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
        public const int MaxSearchLength = 100;

        private readonly ICatalogRepository _repository;
        private readonly TimeProvider _timeProvider;

        public CatalogService(ICatalogRepository repository, TimeProvider timeProvider)
        {
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public PagedResult<SubjectSummaryReadModel> GetSubjects(string? search, int page, int pageSize)
        {
            search = search?.Trim();
            if (string.IsNullOrEmpty(search))
            {
                search = null;
            }
            else if (search.Length > MaxSearchLength)
            {
                search = search[..MaxSearchLength];
            }

            return _repository.GetSubjectSummaries(
                search,
                Math.Max(1, page),
                pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize));
        }

        public SubjectSummaryReadModel? GetSubject(int id) => _repository.GetSubjectSummary(id);

        public IReadOnlyList<DeckSummaryReadModel>? GetDecks(int subjectId, string userId)
        {
            if (!_repository.SubjectExists(subjectId))
            {
                return null;
            }

            return _repository.GetDeckSummaries(subjectId, userId, _timeProvider.GetUtcNow().UtcDateTime);
        }

        public DeckSummaryReadModel? GetDeck(int deckId, string userId)
            => _repository.GetDeckSummary(deckId, userId, _timeProvider.GetUtcNow().UtcDateTime);

        public Task<int[]> GetFavoritesAsync(string userId, FavoriteKind kind, CancellationToken cancellationToken)
            => _repository.GetFavoritesAsync(userId, kind, cancellationToken);

        public Task<bool> SetFavoriteAsync(
            string userId, FavoriteKind kind, int id, bool favorite, CancellationToken cancellationToken)
            => _repository.SetFavoriteAsync(userId, kind, id, favorite, cancellationToken);

        public Task ImportFavoritesAsync(string userId, FavoriteKind kind, int[] ids, CancellationToken cancellationToken)
            => _repository.ImportFavoritesAsync(userId, kind, ids, cancellationToken);
    }
}
