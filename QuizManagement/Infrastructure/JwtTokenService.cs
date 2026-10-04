using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BusinessObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace QuizManagement.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "QuizManagement";

    public string Audience { get; set; } = "QuizManagement.Api";

    // Secret. Required outside Development; in Development a random per-process key is used when unset.
    public string? SigningKey { get; set; }

    // No refresh token: tokens are revoked through SecurityStamp, so a long lifetime is acceptable.
    public int AccessTokenMinutes { get; set; } = 7 * 24 * 60;
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

// Issues bearer tokens for the mobile API. Claims use the same types as the auth cookie, so
// controllers and SignedInUserValidator read both schemes the same way.
public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TimeProvider _timeProvider;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options, SymmetricSecurityKey signingKey, TimeProvider timeProvider)
    {
        _options = options.Value;
        _signingKey = signingKey;
        _timeProvider = timeProvider;
    }

    public AccessToken Issue(User user)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? AppRoles.User),
                new Claim(SignedInUserValidator.SecurityStampClaim, user.SecurityStamp ?? string.Empty)
            ]),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expiresAt);
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions options, SymmetricSecurityKey signingKey)
        => new()
        {
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

    public static SymmetricSecurityKey CreateSigningKey(string? configuredKey)
        => new(string.IsNullOrWhiteSpace(configuredKey)
            ? RandomNumberGenerator.GetBytes(64)
            : Encoding.UTF8.GetBytes(configuredKey));
}
