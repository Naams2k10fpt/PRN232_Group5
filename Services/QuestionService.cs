using BusinessObjects;
using Repositories;

namespace Services
{
    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepository _repository;

        public QuestionService(IQuestionRepository repository)
        {
            _repository = repository;
        }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId)
            => _repository.GetQuestionsByDeckForStudy(deckId);

        public IEnumerable<FlashcardProgress> GetFlashcardProgresses(string userId, int deckId)
            => _repository.GetFlashcardProgresses(userId, deckId);

        public FlashcardProgress ReviewFlashcard(
            string userId,
            int questionId,
            bool remembered,
            DateTime reviewedAtUtc)
            => _repository.ReviewFlashcard(userId, questionId, remembered, reviewedAtUtc);

        public Question? GetQuestionById(int id, string userId, bool allowAll = false)
            => _repository.GetQuestionById(id, userId, allowAll);

        public void AddQuestion(Question question) => _repository.AddQuestion(question);

        public void AddQuestions(IEnumerable<Question> questions) => _repository.AddQuestions(questions);

        public QuestionUpdateResult TryUpdateQuestion(Question question) => _repository.TryUpdateQuestion(question);

        public void DeleteQuestion(Question question) => _repository.DeleteQuestion(question);
    }
}
