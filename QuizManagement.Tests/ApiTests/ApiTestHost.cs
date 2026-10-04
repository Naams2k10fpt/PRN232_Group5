using System.Net.Http.Headers;
using System.Net.Http.Json;
using BusinessObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using QuizManagement.Tests.TestDoubles;
using Services;

namespace QuizManagement.Tests.ApiTests;

// Boots the real Program pipeline (auth schemes, rate limiter, JSON errors) with in-memory account services,
// so /api/* tests never reach a database. One host per test: the rate limiter state is per host.
internal sealed class ApiTestHost : WebApplicationFactory<Program>
{
    public const string Password = "correct horse battery staple";

    public InMemoryUserService Users { get; } = new();
    public EmailSenderFake Emails { get; } = new();
    public LockoutFake Lockout { get; } = new();

    static ApiTestHost()
    {
        // Environment variables override appsettings.Local.json in Program, so a developer's local database
        // is never used. Nothing connects to it: every DB-backed service the API touches is replaced below.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection", "Host=api-tests.invalid;Database=unused");
        Environment.SetEnvironmentVariable("Email__Provider", "Log");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IUserService>(Users);
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<ILoginAttemptService>(Lockout);
            services.AddSingleton<ILoginAttemptLogService, LoginAttemptLogFake>();
            // Real token service, but with in-memory keys instead of the database key ring.
            services.AddSingleton(AccountSecurityFakes.Tokens());
        });
    }

    public User AddUser(string email = "student@example.com", Action<User>? change = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = email.Split('@')[0],
            Email = email,
            Role = AppRoles.User,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
        change?.Invoke(user);
        Users.TryCreateUser(user);
        return user;
    }

    public async Task<string> LoginAsync(HttpClient client, string email = "student@example.com")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    public static void UseToken(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

internal sealed class InMemoryUserService : IUserService
{
    private readonly List<User> _users = [];

    public User? GetByEmail(string email)
        => _users.FirstOrDefault(u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public User? GetByUsername(string username)
        => _users.FirstOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

    public User? GetById(string id) => _users.FirstOrDefault(u => u.Id == id);

    public bool TryCreateUser(User user)
    {
        if (GetByEmail(user.Email) is not null || GetByUsername(user.Username) is not null)
            return false;
        _users.Add(user);
        return true;
    }

    public void UpdateProfile(User user) { }

    public void ChangePassword(User user, string newPasswordHash)
    {
        user.PasswordHash = newPasswordHash;
        user.SecurityStamp = Guid.NewGuid().ToString();
    }
}

internal sealed class LockoutFake : ILoginAttemptService
{
    public TimeSpan? Remaining { get; set; }

    public TimeSpan? GetRemainingLockoutTime(string email, string ipAddress) => Remaining;
}

internal sealed class LoginAttemptLogFake : ILoginAttemptLogService
{
    public void Log(string email, string ipAddress, bool isSuccess, string? userId = null) { }
    public List<LoginAttempt> GetRecent(int count = 200) => [];
    public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success) => Task.FromResult(new List<LoginAttempt>());
}
