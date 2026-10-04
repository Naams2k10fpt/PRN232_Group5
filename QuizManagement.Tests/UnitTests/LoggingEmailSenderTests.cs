using Microsoft.Extensions.Logging;
using QuizManagement.Infrastructure;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_LogsClickableUrlInsteadOfHtmlEncodedUrl()
    {
        var logger = new CapturingLogger();
        var sender = new LoggingEmailSender(logger);

        var sent = await sender.SendAsync(
            "recipient@example.com",
            "Verify email",
            "<a href=\"https://localhost:7133/Account/VerifyEmail?userId=123&amp;token=abc\">Verify</a>");

        Assert.True(sent);
        Assert.Contains(logger.Messages, message =>
            message.Contains("https://localhost:7133/Account/VerifyEmail?userId=123&token=abc"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("&amp;token="));
    }

    private sealed class CapturingLogger : ILogger<LoggingEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
