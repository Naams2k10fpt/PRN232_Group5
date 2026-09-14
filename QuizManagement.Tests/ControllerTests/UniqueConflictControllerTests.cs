using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Controllers;
using QuizManagement.Infrastructure;
using QuizManagement.Tests.TestDoubles;
using QuizManagement.ViewModels.Account;
using QuizManagement.ViewModels.Decks;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class UniqueConflictControllerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_MapsConcurrentUniqueConflictToAGenericError(bool emailConflict)
    {
        var userService = new UserServiceFake(emailConflict);
        var controller = new AccountController(
            userService,
            new LoginAttemptServiceFake(),
            new LoginAttemptLogServiceFake(),
            AccountSecurityFakes.Tokens(),
            new EmailSenderFake());

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "new@test.local",
            Username = "new-user",
            Password = "a sufficiently long passphrase",
            ConfirmPassword = "a sufficiently long passphrase"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<RegisterViewModel>(view.Model);
        Assert.True(controller.ModelState.ContainsKey(string.Empty));
        Assert.False(controller.ModelState.ContainsKey(nameof(RegisterViewModel.Email)));
        Assert.False(controller.ModelState.ContainsKey(nameof(RegisterViewModel.Username)));
        Assert.DoesNotContain(
            controller.ModelState.SelectMany(entry => entry.Value!.Errors),
            error => error.ErrorMessage.Contains("SQL", StringComparison.OrdinalIgnoreCase)
                || error.ErrorMessage.Contains("2601", StringComparison.OrdinalIgnoreCase)
                || error.ErrorMessage.Contains("2627", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, userService.TryCreateCalls);
    }

    [Fact]
    public async Task Register_UsesTheSameGenericErrorForAnExistingIdentity()
    {
        var userService = new UserServiceFake(emailConflict: true, existingBeforeWrite: true);
        var controller = new AccountController(
            userService,
            new LoginAttemptServiceFake(),
            new LoginAttemptLogServiceFake(),
            AccountSecurityFakes.Tokens(),
            new EmailSenderFake());

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "existing@test.local",
            Username = "new-user",
            Password = "a sufficiently long passphrase",
            ConfirmPassword = "a sufficiently long passphrase"
        });

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(string.Empty));
        Assert.False(controller.ModelState.ContainsKey(nameof(RegisterViewModel.Email)));
        Assert.False(controller.ModelState.ContainsKey(nameof(RegisterViewModel.Username)));
        Assert.Equal(0, userService.TryCreateCalls);
    }

    [Fact]
    public void CreateDeck_MapsConcurrentUniqueConflictToNameField()
    {
        var deckService = new DeckServiceFake();
        var controller = new DecksController(new SubjectServiceFake(), deckService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "mentor"),
                        new Claim(ClaimTypes.Name, "mentor"),
                        new Claim(ClaimTypes.Role, AppRoles.Mentor)
                    ], "Test"))
                }
            }
        };

        var result = controller.Create(new DeckFormViewModel
        {
            SubjectId = 7,
            Name = "New deck",
            TimeLimitMinutes = 10
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<DeckFormViewModel>(view.Model);
        Assert.True(controller.ModelState.ContainsKey(nameof(DeckFormViewModel.Name)));
        Assert.Equal(1, deckService.TryAddCalls);
    }

    private sealed class UserServiceFake(bool emailConflict, bool existingBeforeWrite = false) : IUserService
    {
        public int TryCreateCalls { get; private set; }

        public User? GetByEmail(string email)
            => (existingBeforeWrite || TryCreateCalls > 0) && emailConflict ? ExistingUser() : null;

        public User? GetByUsername(string username)
            => (existingBeforeWrite || TryCreateCalls > 0) && !emailConflict ? ExistingUser() : null;

        public bool TryCreateUser(User user)
        {
            TryCreateCalls++;
            return false;
        }

        public User? GetById(string id) => throw new NotSupportedException();
        public void UpdateProfile(User user) => throw new NotSupportedException();
        public void ChangePassword(User user, string newPasswordHash) => throw new NotSupportedException();

        private static User ExistingUser() => new()
        {
            Id = "existing",
            Username = "existing",
            Email = "existing@test.local"
        };
    }

    private sealed class LoginAttemptServiceFake : ILoginAttemptService
    {
        public TimeSpan? GetRemainingLockoutTime(string email, string ipAddress) => null;
    }

    private sealed class LoginAttemptLogServiceFake : ILoginAttemptLogService
    {
        public void Log(string email, string ipAddress, bool isSuccess, string? userId = null)
            => throw new NotSupportedException();

        public List<LoginAttempt> GetRecent(int count = 200) => throw new NotSupportedException();
        public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success) => throw new NotSupportedException();
    }

    private sealed class SubjectServiceFake : ISubjectService
    {
        public Subject? GetSubjectById(int id, string userId, bool allowAll = false)
            => new() { Id = id, UserId = userId, Name = "Subject" };

        public IEnumerable<Subject> GetAllSubjects() => throw new NotSupportedException();
        public Subject? GetSubjectForStudy(int id) => throw new NotSupportedException();
        public bool NameExists(string userId, string name, int? excludedId = null) => throw new NotSupportedException();
        public void AddSubject(Subject subject) => throw new NotSupportedException();
        public void UpdateSubject(Subject subject) => throw new NotSupportedException();
        public void DeleteSubject(Subject subject) => throw new NotSupportedException();
    }

    private sealed class DeckServiceFake : IDeckService
    {
        public int TryAddCalls { get; private set; }

        public bool NameExists(int subjectId, string name, int? excludedId = null) => false;

        public bool TryAddDeck(Deck deck)
        {
            TryAddCalls++;
            return false;
        }

        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckForStudy(int id) => throw new NotSupportedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public bool TryUpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }
}
