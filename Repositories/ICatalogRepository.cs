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
    }
}
