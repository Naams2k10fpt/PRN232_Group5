namespace QuizManagement.Infrastructure;

public sealed class AppOptions
{
    public const string SectionName = "App";

    // Public origin used in emailed links, e.g. https://quiz.onrender.com. When unset, links are
    // built from the incoming request (local development).
    public string? PublicBaseUrl { get; set; }
}

public sealed class HostingOptions
{
    public const string SectionName = "Hosting";

    // True only when the app is reachable exclusively through a TLS-terminating proxy (Render).
    // Trusting X-Forwarded-* anywhere else lets clients spoof their IP address.
    public bool BehindProxy { get; set; }
}

public static class StartupConfigValidator
{
    public static IReadOnlyList<string> Validate(IConfiguration configuration, bool isDevelopment)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
            errors.Add("ConnectionStrings:DefaultConnection chưa được cấu hình.");

        var publicBaseUrl = configuration[$"{AppOptions.SectionName}:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            if (!isDevelopment)
                errors.Add("App:PublicBaseUrl là bắt buộc ngoài Development (ví dụ https://<app>.onrender.com).");
        }
        else if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add("App:PublicBaseUrl phải là URL tuyệt đối http(s).");
        }

        var email = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        switch (email.ResolveProvider())
        {
            case EmailProvider.Log when !isDevelopment:
                errors.Add("Email:Provider=Log chỉ dùng trong Development; hãy đặt Email:Provider=BrevoApi hoặc Smtp.");
                break;
            case EmailProvider.Smtp when string.IsNullOrWhiteSpace(email.Host):
                errors.Add("Email:Provider=Smtp cần Email:Host.");
                break;
            case EmailProvider.BrevoApi:
                if (string.IsNullOrWhiteSpace(email.ApiKey))
                    errors.Add("Email:Provider=BrevoApi cần Email:ApiKey.");
                if (string.IsNullOrWhiteSpace(configuration["Email:FromAddress"]))
                    errors.Add("Email:Provider=BrevoApi cần Email:FromAddress (địa chỉ sender đã xác minh trên Brevo).");
                break;
        }

        return errors;
    }
}
