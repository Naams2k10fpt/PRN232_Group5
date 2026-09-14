using Microsoft.AspNetCore.DataProtection;
using QuizManagement.Infrastructure;

namespace QuizManagement.Tests.TestDoubles;

internal static class AccountSecurityFakes
{
    public static AccountTokenService Tokens()
        => new(new EphemeralDataProtectionProvider());
}

internal sealed class EmailSenderFake : IEmailSender
{
    public List<(string Recipient, string Subject, string Body)> Messages { get; } = [];

    public Task<bool> SendAsync(string recipient, string subject, string htmlBody)
    {
        Messages.Add((recipient, subject, htmlBody));
        return Task.FromResult(true);
    }
}
