using BusinessObjects;
using Microsoft.AspNetCore.Identity;
using Services;

namespace QuizManagement.Infrastructure;

public enum ChangePasswordStatus
{
    Changed,
    WrongCurrentPassword,
    NoPasswordSet
}

// Profile rules shared by the web ProfileController and the mobile ProfileApiController.
public sealed class ProfileService
{
    private readonly IUserService _userService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public ProfileService(IUserService userService)
    {
        _userService = userService;
    }

    // Returns false when another user already has the username.
    public bool TryUpdateProfile(User user, string username, string? avatarUrl)
    {
        username = username.Trim();

        var existing = _userService.GetByUsername(username);
        if (existing is not null && existing.Id != user.Id)
            return false;

        user.Username = username;
        user.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        _userService.UpdateProfile(user);
        return true;
    }

    // On success the SecurityStamp rotates, which signs the user out of every cookie and token.
    public ChangePasswordStatus ChangePassword(User user, string currentPassword, string newPassword)
    {
        if (user.PasswordHash is null)
            return ChangePasswordStatus.NoPasswordSet;

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (result == PasswordVerificationResult.Failed)
            return ChangePasswordStatus.WrongCurrentPassword;

        _userService.ChangePassword(user, _passwordHasher.HashPassword(user, newPassword));
        return ChangePasswordStatus.Changed;
    }
}
