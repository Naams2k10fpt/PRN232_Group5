using BusinessObjects;
using DataAccessObjects;
using System.Collections.Generic;

namespace Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        private readonly QuizManagementDbContext _context;

        public QuestionRepository(QuizManagementDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId)
            => QuestionDAO.Instance.GetQuestionsByDeckForStudy(_context, deckId);

        public IEnumerable<FlashcardProgress> GetFlashcardProgresses(string userId, int deckId)
            => QuestionDAO.Instance.GetFlashcardProgresses(_context, userId, deckId);

        public FlashcardProgress ReviewFlashcard(
            string userId,
            int questionId,
            bool remembered,
            DateTime reviewedAtUtc)
            => QuestionDAO.Instance.ReviewFlashcard(
                _context,
                userId,
                questionId,
                remembered,
                reviewedAtUtc);

        public Question? GetQuestionById(int id, string userId, bool allowAll = false)
            => QuestionDAO.Instance.GetQuestionById(_context, id, userId, allowAll);

        public void AddQuestion(Question question)
            => QuestionDAO.Instance.AddQuestion(_context, question);

        public void AddQuestions(IEnumerable<Question> questions)
            => QuestionDAO.Instance.AddQuestions(_context, questions);

        public QuestionUpdateResult TryUpdateQuestion(Question question)
            => QuestionDAO.Instance.TryUpdateQuestion(_context, question);

        public void DeleteQuestion(Question question)
            => QuestionDAO.Instance.DeleteQuestion(_context, question);
    }
}
