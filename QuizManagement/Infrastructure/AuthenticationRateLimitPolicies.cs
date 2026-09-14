using System.Threading.RateLimiting;

namespace QuizManagement.Infrastructure;

public static class AuthenticationRateLimitPolicies
{
    public const string Login = "account-login";
    public const string Register = "account-register";

    public static RateLimitPartition<string> CreateLoginPartition(HttpContext context)
        => CreateFixedWindowPartition(context, permitLimit: 10);

    public static RateLimitPartition<string> CreateRegisterPartition(HttpContext context)
        => CreateFixedWindowPartition(context, permitLimit: 5);

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        HttpContext context,
        int permitLimit)
        => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
