using System.Net;
using BusinessObjects;
using Microsoft.AspNetCore.Mvc;

namespace QuizManagement.Infrastructure;

// Verification and password-reset emails. Links always point at the web AccountController pages, also when the
// request came from the mobile API: confirming an email or resetting a password happens in the browser.
public sealed class AccountEmails
{
    private readonly AccountTokenService _accountTokenService;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _appOptions;

    public AccountEmails(AccountTokenService accountTokenService, IEmailSender emailSender, AppOptions appOptions)
    {
        _accountTokenService = accountTokenService;
        _emailSender = emailSender;
        _appOptions = appOptions;
    }

    public Task<bool> SendVerificationEmail(User user, IUrlHelper url)
    {
        var token = _accountTokenService.CreateEmailVerificationToken(user);
        var link = AccountLink(url, "VerifyEmail", new { userId = user.Id, token });
        return link is null
            ? Task.FromResult(false)
            : _emailSender.SendAsync(
                user.Email,
                "Xác minh email Quiz Management",
                $"<p>Nhấn vào liên kết sau để xác minh email trong 24 giờ:</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Xác minh email</a></p>");
    }

    public Task<bool> SendPasswordResetEmail(User user, IUrlHelper url)
    {
        var token = _accountTokenService.CreatePasswordResetToken(user);
        var link = AccountLink(url, "ResetPassword", new { userId = user.Id, token });
        return link is null
            ? Task.FromResult(false)
            : _emailSender.SendAsync(
                user.Email,
                "Đặt lại mật khẩu Quiz Management",
                $"<p>Nhấn vào liên kết sau để đặt lại mật khẩu trong 1 giờ:</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Đặt lại mật khẩu</a></p>");
    }

    // Link trong email dùng App:PublicBaseUrl khi có (sau proxy, và không tin Host header của request);
    // local không cấu hình thì dựng từ request như trước.
    private string? AccountLink(IUrlHelper url, string action, object values)
    {
        if (string.IsNullOrWhiteSpace(_appOptions.PublicBaseUrl))
            return url.Action(action, "Account", values, url.ActionContext.HttpContext.Request.Scheme);

        var baseUri = new Uri(_appOptions.PublicBaseUrl);
        return url.Action(action, "Account", values, baseUri.Scheme, baseUri.Authority);
    }
}
