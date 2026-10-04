using System.Security.Claims;
using BusinessObjects;

namespace QuizManagement.Infrastructure;

// One rule for every authentication scheme (cookie and bearer): a signed-in principal stays valid only while
// its user exists, is enabled, has a confirmed email and still has the SecurityStamp it signed in with.
// Changing the password or role rotates the stamp, which signs the user out everywhere.
public static class SignedInUserValidator
{
    public const string SecurityStampClaim = "SecurityStamp";

    public static bool IsStillValid(User? user, ClaimsPrincipal? principal)
    {
        if (user is null || user.IsDisabled || !user.EmailConfirmed)
            return false;

        var stampInPrincipal = principal?.FindFirstValue(SecurityStampClaim);
        return user.SecurityStamp is null || user.SecurityStamp == stampInPrincipal;
    }
}
