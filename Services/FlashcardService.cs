using BusinessObjects;
using Repositories;

namespace Services
{
    // Flashcard study rules shared by the web page and the mobile API.
    public class FlashcardService : IFlashcardService
    {
        private readonly IDeckRepository _deckRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly ICatalogRepository _catalogRepository;
        private readonly TimeProvider _timeProvider;

        public FlashcardService(
            IDeckRepository deckRepository,
            IQuestionRepository questionRepository,
            ICatalogRepository catalogRepository,
            TimeProvider timeProvider)
        {
            _deckRepository = deckRepository;
            _questionRepository = questionRepository;
            _catalogRepository = catalogRepository;
            _timeProvider = timeProvider;
        }

        public FlashcardSession? GetStudySession(string userId, int deckId, bool includeAll)
        {
            var deck = _deckRepository.GetDeckForStudy(deckId);
            if (deck is null)
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var questions = _catalogRepository.GetFlashcardsByDeck(deckId);
            var progressByQuestion = _questionRepository.GetFlashcardProgresses(userId, deckId)
                .ToDictionary(progress => progress.QuestionId);

            bool IsDue(Question question)
                => !progressByQuestion.TryGetValue(question.Id, out var progress)
                    || progress.NextReviewAtUtc <= now;

            // New cards first, then the longest overdue.
            var cards = questions
                .Where(question => includeAll || IsDue(question))
                .OrderBy(question => progressByQuestion.TryGetValue(question.Id, out var progress)
                    ? progress.NextReviewAtUtc
                    : DateTime.MinValue)
                .Select(question =>
                {
                    progressByQuestion.TryGetValue(question.Id, out var progress);
                    var answers = question.Answers.OrderBy(answer => answer.Id).ToList();
                    return new FlashcardCard(
                        question.Id,
                        question.Content,
                        question.QuestionType,
                        question.Explanation,
                        answers.Where(answer => answer.IsCorrect).Select(answer => answer.Content).ToList(),
                        answers.Select(answer => new FlashcardOption(answer.Id, answer.Content, answer.IsCorrect)).ToList(),
                        progress?.Repetition,
                        progress?.NextReviewAtUtc);
                })
                .ToList();

            return new FlashcardSession(
                deck.Id,
                deck.Name,
                deck.Subject.Name,
                questions.Count,
                questions.Count(IsDue),
                includeAll,
                progressByQuestion.Values
                    .Where(progress => progress.NextReviewAtUtc > now)
                    .Select(progress => (DateTime?)progress.NextReviewAtUtc)
                    .Min(),
                cards);
        }

        public FlashcardReviewResult? Review(string userId, int deckId, int questionId, bool remembered)
        {
            if (!_catalogRepository.QuestionBelongsToDeck(deckId, questionId))
            {
                return null;
            }

            var progress = _questionRepository.ReviewFlashcard(
                userId,
                questionId,
                remembered,
                _timeProvider.GetUtcNow().UtcDateTime);
            return new FlashcardReviewResult(
                progress.QuestionId,
                progress.Repetition,
                progress.IntervalMinutes,
                progress.NextReviewAtUtc);
        }
    }
}
