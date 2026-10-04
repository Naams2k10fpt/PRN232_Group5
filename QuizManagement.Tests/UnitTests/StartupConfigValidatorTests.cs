using Microsoft.Extensions.Configuration;
using QuizManagement.Infrastructure;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class StartupConfigValidatorTests
{
    private const string Connection = "Host=localhost;Database=quiz";
    private const string ProductionJwtKey = "a-test-signing-key-that-is-long-enough-1234";

    [Fact]
    public void Development_AllowsLogEmailAndNoPublicUrl()
    {
        var errors = Validate(isDevelopment: true, ("ConnectionStrings:DefaultConnection", Connection));

        Assert.Empty(errors);
    }

    [Fact]
    public void Production_RejectsLogEmail()
    {
        var errors = Validate(
            isDevelopment: false,
            ("ConnectionStrings:DefaultConnection", Connection),
            ("App:PublicBaseUrl", "https://quiz.onrender.com"),
            ("Email:Provider", "Log"));

        Assert.Contains(errors, error => error.Contains("Email:Provider=Log"));
    }

    [Fact]
    public void Production_WithoutAnyEmailSetting_FallsBackToLogAndIsRejected()
    {
        var errors = Validate(
            isDevelopment: false,
            ("ConnectionStrings:DefaultConnection", Connection),
            ("App:PublicBaseUrl", "https://quiz.onrender.com"));

        Assert.Contains(errors, error => error.Contains("Email:Provider=Log"));
    }

    [Fact]
    public void Production_RequiresConnectionStringPublicUrlAndBrevoSettings()
    {
        var errors = Validate(isDevelopment: false, ("Email:Provider", "BrevoApi"));

        Assert.Contains(errors, error => error.Contains("ConnectionStrings:DefaultConnection"));
        Assert.Contains(errors, error => error.Contains("App:PublicBaseUrl"));
        Assert.Contains(errors, error => error.Contains("Email:ApiKey"));
        Assert.Contains(errors, error => error.Contains("Email:FromAddress"));
    }

    [Fact]
    public void Production_CompleteBrevoConfiguration_IsValid()
    {
        var errors = Validate(
            isDevelopment: false,
            ("ConnectionStrings:DefaultConnection", Connection),
            ("App:PublicBaseUrl", "https://quiz.onrender.com"),
            ("Email:Provider", "brevoapi"),
            ("Email:ApiKey", "xkeysib-test"),
            ("Email:FromAddress", "sender@example.com"),
            ("Jwt:SigningKey", ProductionJwtKey));

        Assert.Empty(errors);
    }

    [Fact]
    public void Production_RequiresJwtSigningKey()
    {
        var errors = Validate(isDevelopment: false, ("ConnectionStrings:DefaultConnection", Connection));

        Assert.Contains(errors, error => error.Contains("Jwt:SigningKey"));
    }

    [Fact]
    public void Development_AllowsMissingJwtSigningKey()
    {
        var errors = Validate(isDevelopment: true, ("ConnectionStrings:DefaultConnection", Connection));

        Assert.DoesNotContain(errors, error => error.Contains("Jwt:SigningKey"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void JwtSigningKey_ShorterThan32Bytes_IsRejected(bool isDevelopment)
    {
        var errors = Validate(
            isDevelopment,
            ("ConnectionStrings:DefaultConnection", Connection),
            ("Jwt:SigningKey", "too-short"));

        Assert.Contains(errors, error => error.Contains("Jwt:SigningKey"));
    }

    [Theory]
    [InlineData("quiz.onrender.com")]
    [InlineData("ftp://quiz.onrender.com")]
    public void PublicBaseUrl_MustBeAbsoluteHttpUrl(string value)
    {
        var errors = Validate(
            isDevelopment: true,
            ("ConnectionStrings:DefaultConnection", Connection),
            ("App:PublicBaseUrl", value));

        Assert.Contains(errors, error => error.Contains("App:PublicBaseUrl"));
    }

    [Fact]
    public void SmtpProvider_RequiresHost()
    {
        var errors = Validate(
            isDevelopment: true,
            ("ConnectionStrings:DefaultConnection", Connection),
            ("Email:Provider", "Smtp"));

        Assert.Contains(errors, error => error.Contains("Email:Host"));
    }

    [Theory]
    [InlineData(null, null, EmailProvider.Log)]
    [InlineData(null, "smtp.example.com", EmailProvider.Smtp)]
    [InlineData(EmailProvider.BrevoApi, "smtp.example.com", EmailProvider.BrevoApi)]
    public void ResolveProvider_DefaultsFromHost(EmailProvider? provider, string? host, EmailProvider expected)
    {
        Assert.Equal(expected, new EmailOptions { Provider = provider, Host = host }.ResolveProvider());
    }

    private static IReadOnlyList<string> Validate(bool isDevelopment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting =>
                new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
        return StartupConfigValidator.Validate(configuration, isDevelopment);
    }
}
