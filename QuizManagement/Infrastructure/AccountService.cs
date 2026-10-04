using BusinessObjects;
using Microsoft.AspNetCore.Identity;
using Services;

namespace QuizManagement.Infrastructure;

public enum LoginStatus
{
    Succeeded,
    LockedOut,
    InvalidCredentials,
    AccountDisabled,
    EmailNotConfirmed
}

public sealed record LoginResult(LoginStatus Status, User? User = null, TimeSpan? LockoutRemaining = null);

// Login and registration rules shared by the web AccountController and the mobile AuthApiController, so the
// two front doors cannot drift apart. Callers only turn the result into a view or a JSON response.
public sealed class AccountService
{
    private readonly IUserService _userService;
    private readonly ILoginAttemptService _loginAttemptService;
    private readonly ILoginAttemptLogService _loginAttemptLogService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AccountService(
        IUserService userService,
        ILoginAttemptService loginAttemptService,
        ILoginAttemptLogService loginAttemptLogService)
    {
        _userService = userService;
        _loginAttemptService = loginAttemptService;
        _loginAttemptLogService = loginAttemptLogService;
    }

    public LoginResult Login(string email, string password, string ipAddress)
    {
        email = email.Trim();

        // Check rate limit before any DB query
        var remaining = _loginAttemptService.GetRemainingLockoutTime(email, ipAddress);
        if (remaining is not null)
            return new LoginResult(LoginStatus.LockedOut, LockoutRemaining: remaining);

        var user = _userService.GetByEmail(email);
        if (user?.PasswordHash is null)
        {
            _loginAttemptLogService.Log(email, ipAddress, false, null, countsTowardLockout: true);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _loginAttemptLogService.Log(email, ipAddress, false, user.Id, countsTowardLockout: true);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        if (user.IsDisabled)
        {
            _loginAttemptLogService.Log(email, ipAddress, false, user.Id);
            return new LoginResult(LoginStatus.AccountDisabled);
        }

        if (!user.EmailConfirmed)
        {
            _loginAttemptLogService.Log(email, ipAddress, false, user.Id);
            return new LoginResult(LoginStatus.EmailNotConfirmed);
        }

        _loginAttemptLogService.Log(email, ipAddress, true, user.Id);

        // Ensure SecurityStamp exists for new users migrated from old schema
        if (string.IsNullOrEmpty(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString();
            _userService.UpdateProfile(user);
        }

        return new LoginResult(LoginStatus.Succeeded, user);
    }

    // Returns null when the email or username is taken. Callers must not say which one, to avoid
    // revealing registered accounts.
    public User? Register(string username, string email, string password)
    {
        username = username.Trim();
        email = email.Trim();

        if (_userService.GetByEmail(email) is not null || _userService.GetByUsername(username) is not null)
            return null;

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            Email = email,
            Role = AppRoles.User,
            EmailConfirmed = false,
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        return _userService.TryCreateUser(user) ? user : null;
    }
}
