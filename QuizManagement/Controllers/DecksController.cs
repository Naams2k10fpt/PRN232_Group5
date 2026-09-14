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

        public DecksController(ISubjectService subjectService, IDeckService deckService)
        {
            _subjectService = subjectService;
            _deckService = deckService;
        }

        public IActionResult Index(int subjectId)
        {
            var subject = _subjectService.GetSubjectForStudy(subjectId);
            if (subject is null)
            {
                return NotFound();
            }

            ViewBag.Subject = subject;
            ViewBag.CurrentUserId = CurrentUserId();
            var decks = _deckService.GetDecksBySubjectForStudy(subjectId);
            return View(decks);
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

            if (!_deckService.TryAddDeck(new Deck
            {
                SubjectId = model.SubjectId,
                Name = model.Name,
                TimeLimitMinutes = model.TimeLimitMinutes,
                CreatedBy = User.Identity?.Name
            }))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên bộ đề đã tồn tại trong môn học này.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đã tạo bộ đề.";
            return RedirectToAction(nameof(Index), new { subjectId = model.SubjectId });
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
