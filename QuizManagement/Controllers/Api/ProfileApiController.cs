using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

// Mobile profile. Uses the same ProfileService as the web ProfileController.
[ApiController]
[Route("api/profile")]
[ApiAuthorize]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public class ProfileApiController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ProfileService _profileService;
    private readonly JwtTokenService _jwtTokenService;

    public ProfileApiController(IUserService userService, JwtTokenService jwtTokenService)
    {
        _userService = userService;
        _profileService = new ProfileService(userService);
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>Hồ sơ của người dùng hiện tại. Mobile cũng dùng để kiểm tra token còn hiệu lực.</summary>
    [HttpGet]
    [ProducesResponseType<ApiUser>(StatusCodes.Status200OK)]
    public ActionResult<ApiUser> Get() => ApiUser.From(GetCurrentUser());

    /// <summary>Cập nhật tên người dùng và avatar. Email không đổi được.</summary>
    [HttpPut]
    [ProducesResponseType<ApiUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public ActionResult<ApiUser> Update(UpdateProfileRequest request)
    {
        var user = GetCurrentUser();
        if (!_profileService.TryUpdateProfile(user, request.Username, request.AvatarUrl))
        {
            return BadRequest(new ApiError("validation_failed", "Dữ liệu không hợp lệ.",
                new Dictionary<string, string[]> { ["username"] = ["Tên người dùng đã được sử dụng."] }));
        }

        return ApiUser.From(user);
    }

    /// <summary>
    /// Đổi mật khẩu. Mọi phiên/token cũ bị vô hiệu hóa; response chứa token mới cho thiết bị hiện tại.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public ActionResult<AccessTokenResponse> ChangePassword(ChangePasswordRequest request)
    {
        var user = GetCurrentUser();
        switch (_profileService.ChangePassword(user, request.CurrentPassword, request.NewPassword))
        {
            case ChangePasswordStatus.NoPasswordSet:
                return BadRequest(new ApiError("password_not_supported", "Tài khoản này không hỗ trợ đổi mật khẩu."));
            case ChangePasswordStatus.WrongCurrentPassword:
                return BadRequest(new ApiError("validation_failed", "Dữ liệu không hợp lệ.",
                    new Dictionary<string, string[]> { ["currentPassword"] = ["Mật khẩu hiện tại không đúng."] }));
        }

        var token = _jwtTokenService.Issue(user);
        return new AccessTokenResponse(token.Token, token.ExpiresAt);
    }

    // The bearer handler already rejected deleted users, so a missing user here is a server bug.
    private User GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Không tìm thấy UserId trong token.");
        return _userService.GetById(userId)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng.");
    }
}
