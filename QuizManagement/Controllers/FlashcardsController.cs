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

        public IActionResult Study(int deckId, bool all = false)
        {
            var session = _flashcardService.GetStudySession(CurrentUserId(), deckId, all);
            if (session is null)
            {
                return NotFound();
            }

            var model = new FlashcardStudyViewModel
            {
                DeckId = session.DeckId,
                DeckName = session.DeckName,
                SubjectName = session.SubjectName,
                TotalCards = session.TotalCards,
                DueCards = session.DueCards,
                StudyingAll = session.StudyingAll,
                NextReviewAtUtc = session.NextReviewAtUtc,
                Cards = session.Cards.Select(card => new FlashcardViewModel
                {
                    QuestionId = card.QuestionId,
                    Content = card.Content,
                    Explanation = card.Explanation,
                    QuestionType = card.QuestionType,
                    CorrectAnswers = card.CorrectAnswers.ToList(),
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
        public IActionResult Review(int deckId, int questionId, bool remembered)
        {
            var result = _flashcardService.Review(CurrentUserId(), deckId, questionId, remembered);
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
