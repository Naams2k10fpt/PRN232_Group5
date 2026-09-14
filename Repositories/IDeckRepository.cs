using BusinessObjects;

namespace Repositories
{
    public interface IDeckRepository
    {
        IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId);
        Deck? GetDeckForStudy(int id);
        Deck? GetDeckById(int id, string userId, bool allowAll = false);
        bool NameExists(int subjectId, string name, int? excludedId = null);
        void AddDeck(Deck deck);
        void UpdateDeck(Deck deck);
        void DeleteDeck(Deck deck);
    }
}
