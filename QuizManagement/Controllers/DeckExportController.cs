using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessObjects;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "ManageContent")]
    public class DeckExportController : Controller
    {
        private readonly IDeckService _deckService;
        private readonly IQuestionService _questionService;
        private readonly IDeckExportService _deckExportService;

        public DeckExportController(
            IDeckService deckService,
            IQuestionService questionService,
            IDeckExportService deckExportService)
        {
            _deckService = deckService;
            _questionService = questionService;
            _deckExportService = deckExportService;
        }

        public IActionResult Word(int deckId)
        {
            var deck = _deckService.GetDeckById(deckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            var questions = _questionService.GetQuestionsByDeckForStudy(deckId).ToList();
            var bytes = _deckExportService.ExportDeckToWord(deck, questions);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _deckExportService.BuildSafeFileName(deck.Name, "docx"));
        }

        public IActionResult Pdf(int deckId)
        {
            var deck = _deckService.GetDeckById(deckId, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            var questions = _questionService.GetQuestionsByDeckForStudy(deckId).ToList();
            var bytes = _deckExportService.ExportDeckToPdf(deck, questions);
            return File(bytes, "application/pdf", _deckExportService.BuildSafeFileName(deck.Name, "pdf"));
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private bool IsAdmin() => User.IsInRole(AppRoles.Admin);
    }
}
