using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ViewModels.Admin;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize(Policy = "ManageUsers")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly ILoginAttemptLogService _loginAttemptLogService;

        public AdminController(IAdminService adminService, ILoginAttemptLogService loginAttemptLogService)
        {
            _adminService = adminService;
            _loginAttemptLogService = loginAttemptLogService;
        }

        // GET /Admin — Dashboard tổng quan
        public IActionResult Index()
        {
            var (users, subjects, decks, questions, testHistories) = _adminService.GetSystemStats();
            var model = new AdminDashboardViewModel
            {
                TotalUsers = users,
                TotalSubjects = subjects,
                TotalDecks = decks,
                TotalQuestions = questions,
                TotalTestHistories = testHistories
            };
            return View(model);
        }

        // GET /Admin/Users
        public IActionResult Users(string? search, string? role)
        {
            var users = _adminService.GetAllUsers(search, role);
            var model = new UserListViewModel
            {
                Users = users.Select(ToAdminUserViewModel).ToList(),
                SearchQuery = search,
                RoleFilter = role
            };
            return View(model);
        }

        // GET /Admin/UserDetail/{id}
        public IActionResult UserDetail(string id)
        {
            var user = _adminService.GetUserById(id);
            if (user is null) return NotFound();

            var (subjects, decks, questions, histories) = _adminService.GetUserStats(id);
            var model = new UserDetailViewModel
            {
                User = ToAdminUserViewModel(user),
                SubjectCount = subjects,
                DeckCount = decks,
                QuestionCount = questions,
                TestHistoryCount = histories
            };
            return View(model);
        }

        // GET /Admin/ChangeRole/{id}
        public IActionResult ChangeRole(string id)
        {
            var user = _adminService.GetUserById(id);
            if (user is null) return NotFound();

            return View(new ChangeRoleViewModel
            {
                UserId = user.Id,
                CurrentUsername = user.Username,
                NewRole = user.Role ?? AppRoles.User
            });
        }

        // POST /Admin/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeRole(ChangeRoleViewModel model)
        {
            if (!AppRoles.All.Contains(model.NewRole))
            {
                ModelState.AddModelError(nameof(model.NewRole), "Vai trò không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _adminService.GetUserById(model.UserId);
            if (user is null) return NotFound();

            if (user.Id == CurrentUserId() && model.NewRole != AppRoles.Admin)
            {
                TempData["ErrorMessage"] = "Bạn không thể tự hạ vai trò Admin của mình.";
                return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
            }

            var result = _adminService.ChangeRole(user.Id, model.NewRole);
            if (result == AdminMutationResult.NotFound)
                return NotFound();
            if (result == AdminMutationResult.LastActiveAdmin)
            {
                TempData["ErrorMessage"] = "Hệ thống phải luôn có ít nhất một Admin đang hoạt động.";
                return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
            }

            TempData["SuccessMessage"] = $"Đã đổi vai trò của {user.Username} thành {model.NewRole}.";
            return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
        }

        // POST /Admin/ToggleDisabled
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleDisabled(string id)
        {
            // Admin cannot disable their own account
            if (id == CurrentUserId())
            {
                TempData["ErrorMessage"] = "Bạn không thể tự vô hiệu hóa tài khoản của mình.";
                return RedirectToAction(nameof(UserDetail), new { id });
            }

            var user = _adminService.GetUserById(id);
            if (user is null) return NotFound();

            var result = _adminService.SetDisabled(user.Id, !user.IsDisabled);
            if (result == AdminMutationResult.NotFound)
                return NotFound();
            if (result == AdminMutationResult.LastActiveAdmin)
            {
                TempData["ErrorMessage"] = "Không thể vô hiệu hóa Admin đang hoạt động cuối cùng.";
                return RedirectToAction(nameof(UserDetail), new { id });
            }

            var action = user.IsDisabled ? "vô hiệu hóa" : "kích hoạt";
            TempData["SuccessMessage"] = $"Đã {action} tài khoản {user.Username}.";
            return RedirectToAction(nameof(UserDetail), new { id });
        }

        // GET /Admin/LoginAttempts
        public async Task<IActionResult> LoginAttempts(bool? success)
        {
            var filtered = await _loginAttemptLogService.GetRecentAsync(300, success);
            ViewBag.SuccessFilter = success;
            return View(filtered);
        }

        private string CurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
        }

        private static AdminUserViewModel ToAdminUserViewModel(User user)
            => new()
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                AvatarUrl = user.AvatarUrl,
                IsDisabled = user.IsDisabled,
                CreatedAt = user.CreatedAt
            };
    }
}
