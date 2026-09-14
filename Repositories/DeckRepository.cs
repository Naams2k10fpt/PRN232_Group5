using BusinessObjects;
using DataAccessObjects;
using System.Collections.Generic;

namespace Repositories
{
    public class DeckRepository : IDeckRepository
    {
        private readonly QuizManagementDbContext _context;

        public DeckRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId)
            => DeckDAO.Instance.GetDecksBySubjectForStudy(_context, subjectId);

        public Deck? GetDeckForStudy(int id)
            => DeckDAO.Instance.GetDeckForStudy(_context, id);

        public Deck? GetDeckById(int id, string userId, bool allowAll = false)
            => DeckDAO.Instance.GetDeckById(_context, id, userId, allowAll);

        public bool NameExists(int subjectId, string name, int? excludedId = null)
            => DeckDAO.Instance.NameExists(_context, subjectId, name, excludedId);

        public void AddDeck(Deck deck)
            => DeckDAO.Instance.AddDeck(_context, deck);

        public void UpdateDeck(Deck deck)
            => DeckDAO.Instance.UpdateDeck(_context, deck);

        public void DeleteDeck(Deck deck)
            => DeckDAO.Instance.DeleteDeck(_context, deck);
    }
}
