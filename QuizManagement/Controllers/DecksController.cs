using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Decks;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "StudyContent")]
    public class DecksController : Controller
    {
        private readonly ISubjectService _subjectService;
        private readonly IDeckService _deckService;
        private readonly ICatalogService _catalogService;

        public DecksController(ISubjectService subjectService, IDeckService deckService, ICatalogService catalogService)
        {
            _subjectService = subjectService;
            _deckService = deckService;
            _catalogService = catalogService;
        }

        public async Task<IActionResult> Index(int subjectId, CancellationToken cancellationToken)
        {
            var subject = _subjectService.GetSubjectForStudy(subjectId);
            if (subject is null)
            {
                return NotFound();
            }

            ViewBag.Subject = subject;
            ViewBag.CurrentUserId = CurrentUserId();
            var favorites = (await _catalogService.GetFavoritesAsync(CurrentUserId(), FavoriteKind.Deck, cancellationToken)).ToHashSet();
            ViewBag.Favorites = favorites;
            var decks = _deckService.GetDecksBySubjectForStudy(subjectId);
            return View(decks.OrderByDescending(deck => favorites.Contains(deck.Id)).ToList());
        }

        [Authorize(Policy = "ManageContent")]
        public IActionResult Create(int subjectId)
        {
            var subject = _subjectService.GetSubjectById(subjectId, CurrentUserId(), IsAdmin());
            if (subject is null)
            {
                return NotFound();
            }

            return View(new DeckFormViewModel
            {
                SubjectId = subject.Id,
                SubjectName = subject.Name
            });
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(DeckFormViewModel model)
        {
            model.Name = model.Name.Trim();

            var subject = _subjectService.GetSubjectById(model.SubjectId, CurrentUserId(), IsAdmin());
            if (subject is null)
            {
                return NotFound();
            }

            model.SubjectName = subject.Name;
            if (_deckService.NameExists(model.SubjectId, model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên bộ đề đã tồn tại trong môn học này.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var newDeck = new Deck
            {
                SubjectId = model.SubjectId,
                Name = model.Name,
                TimeLimitMinutes = model.TimeLimitMinutes,
                CreatedBy = User.Identity?.Name
            };

            if (!_deckService.TryAddDeck(newDeck))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên bộ đề đã tồn tại trong môn học này.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo bộ đề \"{newDeck.Name}\"! Bạn có thể thêm câu hỏi hoặc Import câu hỏi ngay bên dưới.";
            return RedirectToAction("Index", "Questions", new { deckId = newDeck.Id });
        }


        [Authorize(Policy = "ManageContent")]
        public IActionResult Edit(int id)
        {
            var deck = _deckService.GetDeckById(id, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            return View(new DeckFormViewModel
            {
                Id = deck.Id,
                SubjectId = deck.SubjectId,
                SubjectName = deck.Subject.Name,
                Name = deck.Name,
                TimeLimitMinutes = deck.TimeLimitMinutes
            });
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, DeckFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            model.Name = model.Name.Trim();

            var deck = _deckService.GetDeckById(id, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            model.SubjectId = deck.SubjectId;
            model.SubjectName = deck.Subject.Name;

            if (_deckService.NameExists(deck.SubjectId, model.Name, id))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên bộ đề đã tồn tại trong môn học này.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            deck.Name = model.Name;
            deck.TimeLimitMinutes = model.TimeLimitMinutes;
            deck.UpdatedBy = User.Identity?.Name;
            if (!_deckService.TryUpdateDeck(deck))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên bộ đề đã tồn tại trong môn học này.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đã cập nhật bộ đề.";
            return RedirectToAction(nameof(Index), new { subjectId = deck.SubjectId });
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var deck = _deckService.GetDeckById(id, CurrentUserId(), IsAdmin());
            if (deck is null)
            {
                return NotFound();
            }

            var subjectId = deck.SubjectId;
            deck.UpdatedBy = User.Identity?.Name;
            _deckService.DeleteDeck(deck);

            TempData["SuccessMessage"] = "Đã xóa bộ đề.";
            return RedirectToAction(nameof(Index), new { subjectId });
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private bool IsAdmin() => User.IsInRole(AppRoles.Admin);
    }
}
