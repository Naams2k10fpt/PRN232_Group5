using System.Security.Claims;
using BusinessObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using QuizManagement.Infrastructure;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class JwtTokenServiceTests
{
    private const string SigningKey = "a-test-signing-key-that-is-long-enough-1234";

    [Fact]
    public async Task Issue_TokenValidatesAndCarriesTheCookieClaims()
    {
        var user = User();
        var options = new JwtOptions { SigningKey = SigningKey };
        var key = JwtTokenService.CreateSigningKey(SigningKey);
        var now = DateTimeOffset.UtcNow;
        var service = new JwtTokenService(Options.Create(options), key, new FixedTimeProvider(now));

        var issued = service.Issue(user);
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }
            .ValidateTokenAsync(issued.Token, JwtTokenService.CreateValidationParameters(options, key));

        Assert.True(result.IsValid, result.Exception?.Message);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.Equal(user.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.Username, principal.Identity!.Name);
        Assert.True(principal.IsInRole(AppRoles.User));
        Assert.Equal(user.SecurityStamp, principal.FindFirstValue(SignedInUserValidator.SecurityStampClaim));
        Assert.Equal(now.AddMinutes(options.AccessTokenMinutes), issued.ExpiresAt);
    }

    [Fact]
    public async Task Token_SignedWithAnotherKey_IsRejected()
    {
        var options = new JwtOptions();
        var service = new JwtTokenService(
            Options.Create(options), JwtTokenService.CreateSigningKey(SigningKey), TimeProvider.System);

        var issued = service.Issue(User());
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            issued.Token,
            JwtTokenService.CreateValidationParameters(options, JwtTokenService.CreateSigningKey("another-signing-key-that-is-long-enough")));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_PastExpiry_IsRejected()
    {
        var options = new JwtOptions { AccessTokenMinutes = 5 };
        var key = JwtTokenService.CreateSigningKey(SigningKey);
        var service = new JwtTokenService(
            Options.Create(options), key, new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(-1)));

        var issued = service.Issue(User());
        var result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(issued.Token, JwtTokenService.CreateValidationParameters(options, key));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MissingKey_GeneratesARandomKeyPerCall()
    {
        var first = JwtTokenService.CreateSigningKey(null);
        var second = JwtTokenService.CreateSigningKey(" ");

        Assert.Equal(64, first.Key.Length);
        Assert.NotEqual(first.Key, second.Key);
    }

    [Fact]
    public void SignedInUserValidator_RejectsDisabledUnconfirmedAndStaleStamp()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(SignedInUserValidator.SecurityStampClaim, "stamp-1")], "test"));

        Assert.True(SignedInUserValidator.IsStillValid(User(), principal));
        Assert.False(SignedInUserValidator.IsStillValid(null, principal));
        Assert.False(SignedInUserValidator.IsStillValid(User(u => u.IsDisabled = true), principal));
        Assert.False(SignedInUserValidator.IsStillValid(User(u => u.EmailConfirmed = false), principal));
        Assert.False(SignedInUserValidator.IsStillValid(User(u => u.SecurityStamp = "stamp-2"), principal));
    }

    private static User User(Action<User>? change = null)
    {
        var user = new User
        {
            Id = "user-1",
            Username = "student",
            Email = "student@example.com",
            Role = AppRoles.User,
            EmailConfirmed = true,
            SecurityStamp = "stamp-1"
        };
        change?.Invoke(user);
        return user;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
