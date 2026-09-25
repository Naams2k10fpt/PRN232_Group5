using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace QuizManagement.Infrastructure;

public enum EmailProvider
{
    // Writes the email (including its link) to the log. Development only.
    Log,
    Smtp,
    // Brevo transactional email over HTTPS (port 443); works where outbound SMTP ports are blocked.
    BrevoApi
}

public sealed class EmailOptions
{
    // When unset: Smtp if Host is configured, otherwise Log (the previous behaviour).
    public EmailProvider? Provider { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
    public string FromAddress { get; set; } = "no-reply@quiz.local";
    public string FromName { get; set; } = "Quiz Management";

    public EmailProvider ResolveProvider()
        => Provider ?? (string.IsNullOrWhiteSpace(Host) ? EmailProvider.Log : EmailProvider.Smtp);
}

public interface IEmailSender
{
    Task<bool> SendAsync(string recipient, string subject, string htmlBody);
}

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(string recipient, string subject, string htmlBody)
    {
        logger.LogInformation(
            "Development email to {Recipient}: {Subject}\n{Body}",
            recipient,
            subject,
            htmlBody);
        return Task.FromResult(true);
    }
}

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    // Fail fast when the SMTP port is blocked instead of holding the request for SmtpClient's 100s default.
    private const int TimeoutMilliseconds = 10_000;

    public async Task<bool> SendAsync(string recipient, string subject, string htmlBody)
    {
        var settings = options.Value;
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(recipient);

            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.EnableSsl,
                Timeout = TimeoutMilliseconds
            };
            if (!string.IsNullOrWhiteSpace(settings.Username))
                client.Credentials = new NetworkCredential(settings.Username, settings.Password);

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not send email to {Recipient}.", recipient);
            return false;
        }
    }
}

public sealed class BrevoApiEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<BrevoApiEmailSender> logger) : IEmailSender
{
    public static readonly Uri Endpoint = new("https://api.brevo.com/v3/smtp/email");

    public async Task<bool> SendAsync(string recipient, string subject, string htmlBody)
    {
        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                sender = new { email = settings.FromAddress, name = settings.FromName },
                to = new[] { new { email = recipient } },
                subject,
                htmlContent = htmlBody
            })
        };
        request.Headers.Add("api-key", settings.ApiKey);
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Brevo rejected email to {Recipient}: {StatusCode} {Body}",
                recipient,
                (int)response.StatusCode,
                body);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(exception, "Could not reach Brevo to send email to {Recipient}.", recipient);
            return false;
        }
    }
}
