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
        private readonly IFlashcardService _flashcardService;

        public FlashcardsController(IFlashcardService flashcardService)
        {
            _flashcardService = flashcardService;
        }

        public IActionResult Study(int deckId, bool all = false, bool missed = false)
        {
            var session = _flashcardService.GetStudySession(CurrentUserId(), deckId, all || missed);
            if (session is null)
            {
                return NotFound();
            }

            var model = new FlashcardStudyViewModel
            {
                SubjectId = session.SubjectId,
                DeckId = session.DeckId,
                DeckName = session.DeckName,
                SubjectName = session.SubjectName,
                TotalCards = session.TotalCards,
                DueCards = session.DueCards,
                StudyingAll = session.StudyingAll,
                StudyingMissed = missed,
                UserId = CurrentUserId(),
                NextReviewAtUtc = session.NextReviewAtUtc,
                Cards = session.Cards.Where(card => !missed || card.Repetition == 0).Select(card => new FlashcardViewModel
                {
                    QuestionId = card.QuestionId,
                    Content = card.Content,
                    Explanation = card.Explanation,
                    QuestionType = card.QuestionType,
                    AllAnswers = card.Options
                        .Select(option => new FlashcardAnswerOption
                        {
                            Content = option.Content,
                            IsCorrect = option.IsCorrect
                        })
                        .ToList()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Review(int deckId, int questionId, bool? remembered, Guid? reviewId = null)
        {
            if (!ModelState.IsValid || remembered is null || deckId <= 0 || questionId <= 0)
                return BadRequest();
            var result = _flashcardService.Review(CurrentUserId(), deckId, questionId, remembered.Value, reviewId);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(new { result.NextReviewAtUtc });
        }

        private string CurrentUserId()
            => User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
    }
}
