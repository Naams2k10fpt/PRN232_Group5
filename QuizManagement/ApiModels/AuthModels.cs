using System.ComponentModel.DataAnnotations;
using BusinessObjects;
using QuizManagement.ViewModels.Account;

namespace QuizManagement.ApiModels;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    public string Password { get; set; } = string.Empty;
}

// Same fields and validation as the web forms, so the API and the website accept exactly the same input.
public sealed class RegisterRequest : RegisterViewModel;

public sealed class EmailRequest : EmailRequestViewModel;

public sealed record ApiUser(
    string Id,
    string Username,
    string Email,
    string Role,
    string? AvatarUrl,
    DateTime CreatedAt)
{
    public static ApiUser From(User user)
        => new(user.Id, user.Username, user.Email, user.Role ?? AppRoles.User, user.AvatarUrl, user.CreatedAt);
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, ApiUser User);

public sealed record MessageResponse(string Message);
