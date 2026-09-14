using BusinessObjects;

namespace Services
{
    public interface IDeckService
    {
        IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId);
        Deck? GetDeckForStudy(int id);
        Deck? GetDeckById(int id, string userId, bool allowAll = false);
        bool NameExists(int subjectId, string name, int? excludedId = null);
        bool TryAddDeck(Deck deck);
        bool TryUpdateDeck(Deck deck);
        void DeleteDeck(Deck deck);
    }
}
