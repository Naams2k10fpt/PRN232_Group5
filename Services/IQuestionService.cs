using BusinessObjects;

namespace Services
{
    public interface IQuestionService
    {
        IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId);
        IEnumerable<FlashcardProgress> GetFlashcardProgresses(string userId, int deckId)
            => throw new NotSupportedException();
        FlashcardProgress ReviewFlashcard(
            string userId,
            int questionId,
            bool remembered,
            DateTime reviewedAtUtc)
            => throw new NotSupportedException();
        Question? GetQuestionById(int id, string userId, bool allowAll = false);
        void AddQuestion(Question question);
        void AddQuestions(IEnumerable<Question> questions);
        QuestionUpdateResult TryUpdateQuestion(Question question);
        void DeleteQuestion(Question question);
    }
}
