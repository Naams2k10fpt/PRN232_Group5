using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Flashcards;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "StudyContent")]
    public class FlashcardsController : Controller
    {
        private readonly IDeckService _deckService;
        private readonly IQuestionService _questionService;
        private readonly TimeProvider _timeProvider;

        public FlashcardsController(
            IDeckService deckService,
            IQuestionService questionService,
            TimeProvider timeProvider)
        {
            _deckService = deckService;
            _questionService = questionService;
            _timeProvider = timeProvider;
        }

        public IActionResult Study(int deckId, bool all = false)
        {
            var deck = _deckService.GetDeckForStudy(deckId);
            if (deck is null)
            {
                return NotFound();
            }

            var userId = CurrentUserId();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var questions = _questionService.GetQuestionsByDeckForStudy(deckId).ToList();
            var progressByQuestion = _questionService.GetFlashcardProgresses(userId, deckId)
                .ToDictionary(progress => progress.QuestionId);
            var dueQuestions = questions
                .Where(question => all
                    || !progressByQuestion.TryGetValue(question.Id, out var progress)
                    || progress.NextReviewAtUtc <= now)
                .OrderBy(question => progressByQuestion.TryGetValue(question.Id, out var progress)
                    ? progress.NextReviewAtUtc
                    : DateTime.MinValue)
                .ToList();
            var model = new FlashcardStudyViewModel
            {
                DeckId = deck.Id,
                DeckName = deck.Name,
                SubjectName = deck.Subject.Name,
                TotalCards = questions.Count,
                DueCards = questions.Count(question => !progressByQuestion.TryGetValue(question.Id, out var progress)
                    || progress.NextReviewAtUtc <= now),
                StudyingAll = all,
                NextReviewAtUtc = progressByQuestion.Values
                    .Where(progress => progress.NextReviewAtUtc > now)
                    .Select(progress => (DateTime?)progress.NextReviewAtUtc)
                    .Min(),
                Cards = dueQuestions.Select(question => new FlashcardViewModel
                {
                    QuestionId = question.Id,
                    Content = question.Content,
                    Explanation = question.Explanation,
                    QuestionType = question.QuestionType,
                    CorrectAnswers = question.Answers
                        .Where(answer => answer.IsCorrect)
                        .OrderBy(answer => answer.Id)
                        .Select(answer => answer.Content)
                        .ToList(),
                    AllAnswers = question.Answers
                        .OrderBy(answer => answer.Id)
                        .Select(answer => new FlashcardAnswerOption
                        {
                            Content = answer.Content,
                            IsCorrect = answer.IsCorrect
                        })
                        .ToList()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Review(int deckId, int questionId, bool remembered)
        {
            if (!_questionService.GetQuestionsByDeckForStudy(deckId)
                    .Any(question => question.Id == questionId))
                return NotFound();

            var progress = _questionService.ReviewFlashcard(
                CurrentUserId(),
                questionId,
                remembered,
                _timeProvider.GetUtcNow().UtcDateTime);
            return Ok(new { progress.NextReviewAtUtc });
        }

        private string CurrentUserId()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
    }
}
