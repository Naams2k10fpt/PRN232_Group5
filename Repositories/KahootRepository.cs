using BusinessObjects;
using DataAccessObjects;
using System.Collections.Generic;

namespace Repositories
{
    public class KahootRepository : IKahootRepository
    {
        private readonly QuizManagementDbContext _context;

        public KahootRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public KahootRoom CreateRoom(KahootRoom room)
            => KahootDAO.Instance.CreateRoom(_context, room);

        public KahootRoom? GetRoomByCode(string roomCode)
            => KahootDAO.Instance.GetRoomByCode(_context, roomCode);

        public KahootRoom? GetRoomById(int id)
            => KahootDAO.Instance.GetRoomById(_context, id);

        public KahootParticipant AddParticipant(KahootParticipant participant)
            => KahootDAO.Instance.AddParticipant(_context, participant);

        public KahootParticipant? GetParticipant(int participantId)
            => KahootDAO.Instance.GetParticipant(_context, participantId);

        public List<KahootParticipant> GetParticipantsByRoom(string roomCode)
            => KahootDAO.Instance.GetParticipantsByRoom(_context, roomCode);

        public void SaveAnswer(KahootAnswer answer)
            => KahootDAO.Instance.SaveAnswer(_context, answer);

        public void UpdateRoomStatus(string roomCode, string status, int currentQuestionIndex)
            => KahootDAO.Instance.UpdateRoomStatus(_context, roomCode, status, currentQuestionIndex);

        public List<Question> GetQuestionsForDeck(int deckId)
            => KahootDAO.Instance.GetQuestionsForDeck(_context, deckId);

        public List<Deck> GetAvailableDecks()
            => KahootDAO.Instance.GetAvailableDecks(_context);
    }
}
