using BusinessObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Infrastructure;
using QuizManagement.ViewModels.Profile;
using Services;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserService _userService;
        private readonly ProfileService _profileService;

        public ProfileController(IUserService userService)
        {
            _userService = userService;
            _profileService = new ProfileService(userService);
        }

        // GET /Profile
        public IActionResult Index()
        {
            var user = GetCurrentUser();
            var model = new ProfileViewModel
            {
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
            return View(model);
        }

        // GET /Profile/Edit
        public IActionResult Edit()
        {
            var user = GetCurrentUser();
            return View(new EditProfileViewModel
            {
                Username = user.Username,
                AvatarUrl = user.AvatarUrl
            });
        }

        // POST /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditProfileViewModel model)
        {
            model.Username = model.Username.Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!_profileService.TryUpdateProfile(GetCurrentUser(), model.Username, model.AvatarUrl))
            {
                ModelState.AddModelError(nameof(model.Username), "Tên người dùng đã được sử dụng.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đã cập nhật hồ sơ.";
            return RedirectToAction(nameof(Index));
        }

        // GET /Profile/ChangePassword
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST /Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            switch (_profileService.ChangePassword(GetCurrentUser(), model.CurrentPassword, model.NewPassword))
            {
                case ChangePasswordStatus.NoPasswordSet:
                    ModelState.AddModelError(string.Empty, "Tài khoản này không hỗ trợ đổi mật khẩu.");
                    return View(model);
                case ChangePasswordStatus.WrongCurrentPassword:
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                    return View(model);
            }

            // Invalidate all sessions by signing out
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
            return RedirectToAction("Login", "Account");
        }

        private User GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Không tìm thấy UserId trong phiên đăng nhập.");
            return _userService.GetById(userId)
                ?? throw new InvalidOperationException("Không tìm thấy người dùng.");
        }
    }
}
