using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

// Mobile login/registration. Uses the same AccountService and AccountEmails as the web AccountController.
// Email verification and password reset links open the web pages.
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AccountService _accountService;
    private readonly AccountEmails _accountEmails;
    private readonly JwtTokenService _jwtTokenService;

    public AuthApiController(
        IUserService userService,
        ILoginAttemptService loginAttemptService,
        ILoginAttemptLogService loginAttemptLogService,
        AccountTokenService accountTokenService,
        IEmailSender emailSender,
        IOptions<AppOptions> appOptions,
        JwtTokenService jwtTokenService)
    {
        _userService = userService;
        _accountService = new AccountService(userService, loginAttemptService, loginAttemptLogService);
        _accountEmails = new AccountEmails(accountTokenService, emailSender, appOptions.Value);
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>Đăng nhập bằng email + mật khẩu, trả về access token (Bearer).</summary>
    [HttpPost("login")]
    [EnableRateLimiting(AuthenticationRateLimitPolicies.Login)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = _accountService.Login(request.Email, request.Password, ipAddress);

        switch (result.Status)
        {
            case LoginStatus.LockedOut:
                var seconds = (int)Math.Ceiling(result.LockoutRemaining!.Value.TotalSeconds);
                Response.Headers.RetryAfter = seconds.ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests, new ApiError(
                    "locked_out",
                    $"Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Thử lại sau {(int)Math.Ceiling(seconds / 60.0)} phút.",
                    RetryAfterSeconds: seconds));
            case LoginStatus.InvalidCredentials:
                return Unauthorized(new ApiError("invalid_credentials", "Email hoặc mật khẩu không đúng."));
            case LoginStatus.AccountDisabled:
                return StatusCode(StatusCodes.Status403Forbidden, new ApiError(
                    "account_disabled", "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên."));
            case LoginStatus.EmailNotConfirmed:
                return StatusCode(StatusCodes.Status403Forbidden, new ApiError(
                    "email_not_confirmed", "Email chưa được xác minh. Vui lòng kiểm tra hộp thư hoặc gửi lại email xác minh."));
        }

        var user = result.User!;
        var token = _jwtTokenService.Issue(user);
        return Ok(new LoginResponse(token.Token, token.ExpiresAt, ApiUser.From(user)));
    }

    /// <summary>Đăng ký tài khoản. Không trả token: phải xác minh email trước khi đăng nhập.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Register(RegisterRequest request)
    {
        var user = _accountService.Register(request.Username, request.Email, request.Password);
        if (user is null)
        {
            return Conflict(new ApiError(
                "registration_conflict",
                "Không thể đăng ký với thông tin này. Vui lòng kiểm tra lại hoặc dùng thông tin khác."));
        }

        var sent = await _accountEmails.SendVerificationEmail(user, Url);
        return StatusCode(StatusCodes.Status201Created, new MessageResponse(sent
            ? "Đăng ký thành công. Vui lòng kiểm tra email để xác minh tài khoản."
            : "Đăng ký thành công nhưng chưa gửi được email. Vui lòng dùng chức năng gửi lại email xác minh."));
    }

    /// <summary>Gửi lại email xác minh. Luôn trả 202 để không lộ email nào đã đăng ký.</summary>
    [HttpPost("resend-verification")]
    [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> ResendVerification(EmailRequest request)
    {
        var user = _userService.GetByEmail(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed && !user.IsDisabled)
            await _accountEmails.SendVerificationEmail(user, Url);

        return Accepted(new MessageResponse("Nếu tài khoản tồn tại và chưa được xác minh, email mới đã được gửi."));
    }

    /// <summary>Gửi email đặt lại mật khẩu (link mở trang web). Luôn trả 202.</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(AuthenticationRateLimitPolicies.Register)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(EmailRequest request)
    {
        var user = _userService.GetByEmail(request.Email.Trim());
        if (user is not null && user.EmailConfirmed && !user.IsDisabled)
            await _accountEmails.SendPasswordResetEmail(user, Url);

        return Accepted(new MessageResponse("Nếu tài khoản hợp lệ, email đặt lại mật khẩu đã được gửi."));
    }
}
