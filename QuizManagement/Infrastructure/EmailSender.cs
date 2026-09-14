using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace QuizManagement.Infrastructure;

public sealed class EmailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@quiz.local";
    public string FromName { get; set; } = "Quiz Management";
}

public interface IEmailSender
{
    Task<bool> SendAsync(string recipient, string subject, string htmlBody);
}

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    IHostEnvironment environment,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(string recipient, string subject, string htmlBody)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation(
                    "Development email to {Recipient}: {Subject}\n{Body}",
                    recipient,
                    subject,
                    htmlBody);
                return true;
            }

            logger.LogError("Email:Host is required outside Development.");
            return false;
        }

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
                EnableSsl = settings.EnableSsl
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
