using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.Admin;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class AdminControllerTests
{
    [Fact]
    public void ChangeRole_ShowsSafeErrorWhenServiceProtectsLastActiveAdmin()
    {
        var service = new AdminServiceFake
        {
            User = new User { Id = "admin-a", Username = "Admin A", Role = AppRoles.Admin },
            ChangeRoleResult = AdminMutationResult.LastActiveAdmin
        };
        var controller = BuildController(service);

        var result = controller.ChangeRole(new ChangeRoleViewModel
        {
            UserId = "admin-a",
            CurrentUsername = "Admin A",
            NewRole = AppRoles.Mentor
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("UserDetail", redirect.ActionName);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        Assert.Equal(("admin-a", AppRoles.Mentor), service.LastRoleChange);
    }

    [Fact]
    public void UserListAndDetail_ProjectOnlyFieldsNeededByAdminViews()
    {
        var entity = new User
        {
            Id = "user-a",
            Username = "User A",
            Email = "user-a@test.local",
            PasswordHash = "must-not-cross-view-boundary",
            SecurityStamp = "must-not-cross-view-boundary",
            Role = AppRoles.User,
            AvatarUrl = "/avatar.png",
            CreatedAt = DateTime.UtcNow
        };
        var service = new AdminServiceFake { User = entity, Users = [entity] };
        var controller = BuildController(service);

        var list = Assert.IsType<UserListViewModel>(Assert.IsType<ViewResult>(controller.Users(null, null)).Model);
        var detail = Assert.IsType<UserDetailViewModel>(Assert.IsType<ViewResult>(controller.UserDetail(entity.Id)).Model);

        Assert.IsType<AdminUserViewModel>(Assert.Single(list.Users));
        Assert.IsType<AdminUserViewModel>(detail.User);
        Assert.Equal(entity.Email, detail.User.Email);
        Assert.DoesNotContain(
            typeof(AdminUserViewModel).GetProperties(),
            property => property.Name is nameof(User.PasswordHash) or nameof(User.SecurityStamp));
        Assert.DoesNotContain(
            typeof(UserListViewModel).GetProperties().Concat(typeof(UserDetailViewModel).GetProperties()),
            property => typeof(User).IsAssignableFrom(property.PropertyType));
    }

    private static AdminController BuildController(IAdminService service)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "admin-current"),
                new Claim(ClaimTypes.Role, AppRoles.Admin)
            ], "Test"))
        };

        return new AdminController(service, new LoginAttemptLogServiceFake())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TempDataProviderFake())
        };
    }

    private sealed class AdminServiceFake : IAdminService
    {
        public User? User { get; init; }
        public List<User> Users { get; init; } = [];
        public AdminMutationResult ChangeRoleResult { get; init; }
        public (string UserId, string Role)? LastRoleChange { get; private set; }

        public User? GetUserById(string id) => User;
        public AdminMutationResult ChangeRole(string userId, string newRole)
        {
            LastRoleChange = (userId, newRole);
            return ChangeRoleResult;
        }

        public AdminMutationResult SetDisabled(string userId, bool disabled) => throw new NotSupportedException();
        public (int users, int subjects, int decks, int questions, int testHistories) GetSystemStats() => throw new NotSupportedException();
        public List<User> GetAllUsers(string? search = null, string? roleFilter = null) => Users;
        public (int subjects, int decks, int questions, int testHistories) GetUserStats(string userId) => (0, 0, 0, 0);
    }

    private sealed class LoginAttemptLogServiceFake : ILoginAttemptLogService
    {
        public void Log(string email, string ipAddress, bool isSuccess, string? userId = null) => throw new NotSupportedException();
        public List<LoginAttempt> GetRecent(int count = 200) => throw new NotSupportedException();
        public Task<List<LoginAttempt>> GetRecentAsync(int count, bool? success) => throw new NotSupportedException();
    }

    private sealed class TempDataProviderFake : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
