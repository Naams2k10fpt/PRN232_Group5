using System.Security.Cryptography;
using System.Text;
using BusinessObjects;
using Microsoft.AspNetCore.DataProtection;

namespace QuizManagement.Infrastructure;

public sealed class AccountTokenService(IDataProtectionProvider provider)
{
    private static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    public string CreateEmailVerificationToken(User user)
        => Create(user, "email-verification-v1", EmailVerificationLifetime);

    public bool ValidateEmailVerificationToken(User user, string token)
        => Validate(user, token, "email-verification-v1");

    public string CreatePasswordResetToken(User user)
        => Create(user, "password-reset-v1", PasswordResetLifetime);

    public bool ValidatePasswordResetToken(User user, string token)
        => Validate(user, token, "password-reset-v1");

    private string Create(User user, string purpose, TimeSpan lifetime)
        => Protector(purpose).Protect(Payload(user), lifetime);

    private bool Validate(User user, string token, string purpose)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var actual = Encoding.UTF8.GetBytes(Protector(purpose).Unprotect(token));
            var expected = Encoding.UTF8.GetBytes(Payload(user));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private ITimeLimitedDataProtector Protector(string purpose)
        => provider.CreateProtector($"QuizManagement.Account.{purpose}").ToTimeLimitedDataProtector();

    private static string Payload(User user) => $"{user.Id}\n{user.SecurityStamp}";
}
