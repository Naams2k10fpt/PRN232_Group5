using BusinessObjects;

namespace Repositories
{
    public interface IQuestionRepository
    {
        IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId);
        IEnumerable<FlashcardProgress> GetFlashcardProgresses(string userId, int deckId);
        FlashcardProgress ReviewFlashcard(
            string userId,
            int questionId,
            bool remembered,
            DateTime reviewedAtUtc);
        Question? GetQuestionById(int id, string userId, bool allowAll = false);
        void AddQuestion(Question question);
        void AddQuestions(IEnumerable<Question> questions);
        QuestionUpdateResult TryUpdateQuestion(Question question);
        void DeleteQuestion(Question question);
    }
}
