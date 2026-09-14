using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Subjects;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "StudyContent")]
    public class SubjectsController : Controller
    {
        private readonly ISubjectService _subjectService;

        public SubjectsController(ISubjectService subjectService)
        {
            _subjectService = subjectService;
        }

        public IActionResult Index()
        {
            ViewBag.CurrentUserId = CurrentUserId();
            var subjects = _subjectService.GetAllSubjects();
            return View(subjects);
        }

        [Authorize(Policy = "ManageContent")]
        public IActionResult Create()
        {
            return View(new SubjectFormViewModel());
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SubjectFormViewModel model)
        {
            model.Name = model.Name.Trim();

            if (_subjectService.NameExists(CurrentUserId(), model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên môn học đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _subjectService.AddSubject(new Subject
            {
                UserId = CurrentUserId(),
                Name = model.Name,
                CreatedBy = User.Identity?.Name
            });

            TempData["SuccessMessage"] = "Đã tạo môn học.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "ManageContent")]
        public IActionResult Edit(int id)
        {
            var subject = _subjectService.GetSubjectById(id, CurrentUserId(), IsAdmin());
            if (subject is null)
            {
                return NotFound();
            }

            return View(new SubjectFormViewModel
            {
                Id = subject.Id,
                Name = subject.Name
            });
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, SubjectFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            model.Name = model.Name.Trim();

            var subject = _subjectService.GetSubjectById(id, CurrentUserId(), IsAdmin());
            if (subject is null)
            {
                return NotFound();
            }

            if (_subjectService.NameExists(subject.UserId, model.Name, id))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên môn học đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            subject.Name = model.Name;
            subject.UpdatedBy = User.Identity?.Name;
            _subjectService.UpdateSubject(subject);

            TempData["SuccessMessage"] = "Đã cập nhật môn học.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = "ManageContent")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var subject = _subjectService.GetSubjectById(id, CurrentUserId(), IsAdmin());
            if (subject is null)
            {
                return NotFound();
            }

            subject.UpdatedBy = User.Identity?.Name;
            _subjectService.DeleteSubject(subject);

            TempData["SuccessMessage"] = "Đã xóa môn học.";
            return RedirectToAction(nameof(Index));
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private bool IsAdmin() => User.IsInRole(AppRoles.Admin);
    }
}
