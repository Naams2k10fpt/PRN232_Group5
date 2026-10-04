using Microsoft.EntityFrameworkCore;
using BusinessObjects;
using Repositories;
using Services;
using DataAccessObjects;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using QuizManagement.Infrastructure;
using QuizManagement.Hubs;
using QuizManagement.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình file config local (nếu có) và logging.
// Biến môi trường/tham số dòng lệnh được nạp lại sau file local để luôn được ưu tiên (Render, docker compose).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Dừng ngay khi cấu hình triển khai thiếu/sai, thay vì chạy nửa vời.
var configErrors = StartupConfigValidator.Validate(builder.Configuration, builder.Environment.IsDevelopment());
if (configErrors.Count > 0)
    throw new InvalidOperationException("Cấu hình không hợp lệ:\n- " + string.Join("\n- ", configErrors));

// Render (và các PaaS tương tự) chỉ định cổng qua biến PORT; local vẫn dùng launchSettings.json.
var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var hosting = builder.Configuration.GetSection(HostingOptions.SectionName).Get<HostingOptions>() ?? new HostingOptions();

// 1. DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<QuizManagementDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Repositories
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IDeckRepository, DeckRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IQuestionReportRepository, QuestionReportRepository>();
builder.Services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
builder.Services.AddScoped<IKahootRepository, KahootRepository>();

// 3. Services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IDeckService, DeckService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IQuestionImportService, QuestionImportService>();
builder.Services.AddScoped<IQuestionReportService, QuestionReportService>();
builder.Services.AddScoped<ILoginAttemptLogService, LoginAttemptLogService>();
builder.Services.AddSingleton<IDeckExportService, DeckExportService>();
builder.Services.AddScoped<IKahootService, KahootService>();
builder.Services.AddScoped<StudyGroupService>();

// 4. Infrastructure (web-tier)
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddSingleton<AccountTokenService>();
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
var emailProvider = (builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions()).ResolveProvider();
switch (emailProvider)
{
    case EmailProvider.BrevoApi:
        builder.Services.AddHttpClient<IEmailSender, BrevoApiEmailSender>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));
        break;
    case EmailProvider.Smtp:
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        break;
    default:
        builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
        break;
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        AuthenticationRateLimitPolicies.Login,
        AuthenticationRateLimitPolicies.CreateLoginPartition);
    options.AddPolicy(
        AuthenticationRateLimitPolicies.Register,
        AuthenticationRateLimitPolicies.CreateRegisterPartition);
});

// 5. Data Protection: khóa lưu trong PostgreSQL để cookie đăng nhập và link xác minh/đặt lại mật khẩu
// vẫn hợp lệ sau restart, redeploy hoặc khi container free tier bị ngủ/đánh thức (đĩa bị xóa).
builder.Services.AddDataProtection()
    .SetApplicationName("QuizManagement")
    .PersistKeysToDbContext<QuizManagementDbContext>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<QuizManagementDbContext>();

if (hosting.BehindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        // Render không có dải IP proxy cố định; chỉ an toàn vì container chỉ nhận request qua proxy.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// 6. Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // ManageUsers: chỉ Admin
    options.AddPolicy("ManageUsers", policy =>
        policy.RequireRole(AppRoles.Admin));

    // Admin quản lý toàn bộ học liệu; Mentor chỉ quản lý học liệu thuộc sở hữu của mình.
    options.AddPolicy("ManageContent", policy =>
        policy.RequireRole(AppRoles.Admin, AppRoles.Mentor));

    // Cả ba vai trò đều có thể duyệt học liệu, học flashcard và làm quiz.
    options.AddPolicy("StudyContent", policy =>
        policy.RequireRole(AppRoles.Admin, AppRoles.Mentor, AppRoles.User));

    // Thống kê được lọc theo user hiện tại tại controller/service.
    options.AddPolicy("ViewAnalytics", policy =>
        policy.RequireAuthenticatedUser());

});

// 7. Cookie Authentication với SecurityStamp + IsDisabled validation
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId is null) return Task.CompletedTask;

                var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                var user = userService.GetById(userId);

                // Reject if user deleted or disabled
                if (user is null || user.IsDisabled || !user.EmailConfirmed)
                {
                    context.RejectPrincipal();
                    return Task.CompletedTask;
                }

                // Reject if SecurityStamp has changed (password changed / role changed)
                var stampInCookie = context.Principal?.FindFirstValue("SecurityStamp");
                if (user.SecurityStamp is not null && user.SecurityStamp != stampInCookie)
                {
                    context.RejectPrincipal();
                }

                return Task.CompletedTask;
            }
        };
    });

// 8. MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Chỉ tự áp dụng migration khi được bật rõ ràng (một instance duy nhất, ví dụ bản deploy free tier).
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<QuizManagementDbContext>().Database.Migrate();
}

// Chỉ seed Admin khi được bật rõ ràng trong appsettings.Local.json/environment.
if (builder.Configuration.GetValue<bool>("AdminSeed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var username = builder.Configuration["AdminSeed:Username"]?.Trim();
    var email = builder.Configuration["AdminSeed:Email"]?.Trim();
    var password = builder.Configuration["AdminSeed:Password"];

    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password) ||
        password.Length is < PasswordPolicy.MinimumLength or > PasswordPolicy.MaximumLength ||
        password == "replace-with-a-strong-password")
    {
        throw new InvalidOperationException(
            "AdminSeed được bật nhưng Username, Email hoặc Password (8–100 ký tự) chưa hợp lệ.");
    }

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<QuizManagementDbContext>();
        if (!context.Users.Any(u => u.Role == AppRoles.Admin))
        {
            var normalizedEmail = TextNormalization.Normalize(email);
            var normalizedUsername = TextNormalization.Normalize(username);
            if (context.Users.Any(u => EF.Property<string>(u, "NormalizedEmail") == normalizedEmail
                || EF.Property<string>(u, "NormalizedUsername") == normalizedUsername))
            {
                throw new InvalidOperationException(
                    "Không thể seed Admin vì Username hoặc Email đã được sử dụng.");
            }

            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            var adminUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                Email = email,
                Role = AppRoles.Admin,
                IsDisabled = false,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            adminUser.PasswordHash = hasher.HashPassword(adminUser, password);
            context.Users.Add(adminUser);
            context.SaveChanges();
            logger.LogInformation("Đã tạo tài khoản Admin cấu hình cho {AdminEmail}.", email);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Không thể seed tài khoản Admin.");
        throw;
    }
}

// Phải chạy đầu tiên để IP client, scheme HTTPS và HSTS phản ánh request gốc qua proxy.
if (hosting.BehindProxy)
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Sau proxy, TLS kết thúc tại Render và Render tự chuyển HTTP → HTTPS.
if (!hosting.BehindProxy)
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<KahootHub>("/hubs/kahoot");
app.MapHub<ChatHub>("/hubs/chat");
app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
