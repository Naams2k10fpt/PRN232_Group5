using BusinessObjects;
using System.Collections.Generic;

namespace Repositories
{
    public interface IKahootRepository
    {
        KahootRoom CreateRoom(KahootRoom room);
        KahootRoom? GetRoomByCode(string roomCode);
        KahootRoom? GetRoomById(int id);
        KahootParticipant AddParticipant(KahootParticipant participant);
        KahootParticipant? GetParticipant(int participantId);
        List<KahootParticipant> GetParticipantsByRoom(string roomCode);
        KahootAnswer? GetAnswer(int participantId, int questionId);
        void SaveAnswer(KahootAnswer answer);
        void UpdateRoomStatus(string roomCode, string status, int currentQuestionIndex);
        List<Question> GetQuestionsForDeck(int deckId);
        List<Deck> GetAvailableDecks();
    }
}
