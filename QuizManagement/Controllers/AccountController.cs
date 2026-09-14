using BusinessObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuizManagement.Infrastructure;
using QuizManagement.ViewModels.Account;
using Services;
using System.Net;
using System.Security.Claims;

namespace QuizManagement.Controllers
{
    public class AccountController : Controller
    {
        private const string RegistrationConflictMessage =
            "Không thể đăng ký với thông tin này. Vui lòng kiểm tra lại hoặc dùng thông tin khác.";
        private readonly IUserService _userService;
        private readonly ILoginAttemptService _loginAttemptService;
        private readonly ILoginAttemptLogService _loginAttemptLogService;
        private readonly AccountTokenService _accountTokenService;
        private readonly IEmailSender _emailSender;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AccountController(
            IUserService userService,
            ILoginAttemptService loginAttemptService,
            ILoginAttemptLogService loginAttemptLogService,
            AccountTokenService accountTokenService,
            IEmailSender emailSender)
        {
            _userService = userService;
            _loginAttemptService = loginAttemptService;
            _loginAttemptLogService = loginAttemptLogService;
            _accountTokenService = accountTokenService;
            _emailSender = emailSender;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            model.Email = model.Email.Trim();
            model.Username = model.Username.Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var emailExists = _userService.GetByEmail(model.Email) is not null;
            var usernameExists = _userService.GetByUsername(model.Username) is not null;
            if (emailExists || usernameExists)
            {
                ModelState.AddModelError(string.Empty, RegistrationConflictMessage);
                return View(model);
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = model.Username,
                Email = model.Email,
                Role = AppRoles.User,
                EmailConfirmed = false,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
            if (!_userService.TryCreateUser(user))
            {
                ModelState.AddModelError(string.Empty, RegistrationConflictMessage);
                return View(model);
            }

            var sent = await SendVerificationEmail(user);
            TempData["SuccessMessage"] = sent
                ? "Đăng ký thành công. Vui lòng kiểm tra email để xác minh tài khoản."
                : "Đăng ký thành công nhưng chưa gửi được email. Vui lòng dùng chức năng gửi lại email xác minh.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResendVerification() => View(new EmailRequestViewModel());

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendVerification(EmailRequestViewModel model)
        {
            model.Email = model.Email.Trim();
            if (!ModelState.IsValid)
                return View(model);

            var user = _userService.GetByEmail(model.Email);
            if (user is not null && !user.EmailConfirmed && !user.IsDisabled)
                await SendVerificationEmail(user);

            TempData["SuccessMessage"] = "Nếu tài khoản tồn tại và chưa được xác minh, email mới đã được gửi.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyEmail(string userId, string token)
        {
            var user = _userService.GetById(userId);
            if (user is null || (!_accountTokenService.ValidateEmailVerificationToken(user, token)
                && !user.EmailConfirmed))
            {
                TempData["ErrorMessage"] = "Liên kết xác minh không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction(nameof(Login));
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                user.SecurityStamp = Guid.NewGuid().ToString();
                _userService.UpdateProfile(user);
            }

            TempData["SuccessMessage"] = "Email đã được xác minh. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View(new EmailRequestViewModel());

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(EmailRequestViewModel model)
        {
            model.Email = model.Email.Trim();
            if (!ModelState.IsValid)
                return View(model);

            var user = _userService.GetByEmail(model.Email);
            if (user is not null && user.EmailConfirmed && !user.IsDisabled)
                await SendPasswordResetEmail(user);

            TempData["SuccessMessage"] = "Nếu tài khoản hợp lệ, email đặt lại mật khẩu đã được gửi.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            var user = _userService.GetById(userId);
            if (user is null || !_accountTokenService.ValidatePasswordResetToken(user, token))
            {
                TempData["ErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordViewModel { UserId = userId, Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _userService.GetById(model.UserId);
            if (user is null || user.IsDisabled
                || !_accountTokenService.ValidatePasswordResetToken(user, model.Token))
            {
                ModelState.AddModelError(string.Empty, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            _userService.ChangePassword(user, _passwordHasher.HashPassword(user, model.Password));
            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(AuthenticationRateLimitPolicies.Login)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            model.Email = model.Email.Trim();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Check rate limit before any DB query
            var remaining = _loginAttemptService.GetRemainingLockoutTime(model.Email, ipAddress);
            if (remaining is not null)
            {
                var minutes = (int)Math.Ceiling(remaining.Value.TotalMinutes);
                ModelState.AddModelError(string.Empty,
                    $"Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Thử lại sau {minutes} phút.");
                return View(model);
            }

            var user = _userService.GetByEmail(model.Email);
            if (user?.PasswordHash is null)
            {
                _loginAttemptLogService.Log(model.Email, ipAddress, false, null, countsTowardLockout: true);
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                _loginAttemptLogService.Log(model.Email, ipAddress, false, user.Id, countsTowardLockout: true);
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            if (user.IsDisabled)
            {
                _loginAttemptLogService.Log(model.Email, ipAddress, false, user.Id);
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                _loginAttemptLogService.Log(model.Email, ipAddress, false, user.Id);
                ModelState.AddModelError(string.Empty, "Email chưa được xác minh. Vui lòng kiểm tra hộp thư hoặc gửi lại email xác minh.");
                return View(model);
            }

            _loginAttemptLogService.Log(model.Email, ipAddress, true, user.Id);

            // Ensure SecurityStamp exists for new users migrated from old schema
            if (string.IsNullOrEmpty(user.SecurityStamp))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                _userService.UpdateProfile(user);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role ?? AppRoles.User),
                new("SecurityStamp", user.SecurityStamp)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private Task<bool> SendVerificationEmail(User user)
        {
            var token = _accountTokenService.CreateEmailVerificationToken(user);
            var url = Url.Action(nameof(VerifyEmail), "Account", new { userId = user.Id, token }, Request.Scheme);
            return url is null
                ? Task.FromResult(false)
                : _emailSender.SendAsync(
                    user.Email,
                    "Xác minh email Quiz Management",
                    $"<p>Nhấn vào liên kết sau để xác minh email trong 24 giờ:</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Xác minh email</a></p>");
        }

        private Task<bool> SendPasswordResetEmail(User user)
        {
            var token = _accountTokenService.CreatePasswordResetToken(user);
            var url = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, token }, Request.Scheme);
            return url is null
                ? Task.FromResult(false)
                : _emailSender.SendAsync(
                    user.Email,
                    "Đặt lại mật khẩu Quiz Management",
                    $"<p>Nhấn vào liên kết sau để đặt lại mật khẩu trong 1 giờ:</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Đặt lại mật khẩu</a></p>");
        }
    }
}
