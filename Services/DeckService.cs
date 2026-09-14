using BusinessObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories;

namespace Services
{
    public class DeckService : IDeckService
    {
        private readonly IDeckRepository _repository;

        public DeckService(IDeckRepository repository)
        {
            _repository = repository;
        }

        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId)
            => _repository.GetDecksBySubjectForStudy(subjectId);


        public Deck? GetDeckForStudy(int id) => _repository.GetDeckForStudy(id);

        public Deck? GetDeckById(int id, string userId, bool allowAll = false)
            => _repository.GetDeckById(id, userId, allowAll);

        public bool NameExists(int subjectId, string name, int? excludedId = null) => _repository.NameExists(subjectId, name, excludedId);

        public bool TryAddDeck(Deck deck)
        {
            try
            {
                _repository.AddDeck(deck);
                return true;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return false;
            }
        }

        public bool TryUpdateDeck(Deck deck)
        {
            try
            {
                _repository.UpdateDeck(deck);
                return true;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return false;
            }
        }

        public void DeleteDeck(Deck deck) => _repository.DeleteDeck(deck);
    }
}
