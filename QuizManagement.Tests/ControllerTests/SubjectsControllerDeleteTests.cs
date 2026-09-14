using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuizManagement.Controllers;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class SubjectsControllerDeleteTests
{
    [Fact]
    public void Delete_ReturnsNotFoundWithoutDeleting_WhenMentorDoesNotOwnSubject()
    {
        var service = new SubjectServiceFake();
        var controller = BuildController(service, AppRoles.Mentor, "mentor-a");

        var result = controller.Delete(42);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal((42, "mentor-a", false), service.LastLookup);
        Assert.Equal(0, service.DeleteCalls);
    }

    [Fact]
    public void Delete_AllowsAdminScope()
    {
        var service = new SubjectServiceFake { Subject = new Subject { Id = 42 } };
        var controller = BuildController(service, AppRoles.Admin, "admin-a");

        var result = controller.Delete(42);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal((42, "admin-a", true), service.LastLookup);
        Assert.Equal(1, service.DeleteCalls);
    }

    private static SubjectsController BuildController(SubjectServiceFake service, string role, string userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            ], "Test"))
        };

        return new SubjectsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TempDataProviderFake())
        };
    }

    private sealed class SubjectServiceFake : ISubjectService
    {
        public Subject? Subject { get; init; }
        public (int Id, string UserId, bool AllowAll) LastLookup { get; private set; }
        public int DeleteCalls { get; private set; }

        public Subject? GetSubjectById(int id, string userId, bool allowAll = false)
        {
            LastLookup = (id, userId, allowAll);
            return Subject;
        }

        public void DeleteSubject(Subject subject) => DeleteCalls++;
        public IEnumerable<Subject> GetAllSubjects() => throw new NotSupportedException();
        public Subject? GetSubjectForStudy(int id) => throw new NotSupportedException();
        public bool NameExists(string userId, string name, int? excludedId = null) => throw new NotSupportedException();
        public void AddSubject(Subject subject) => throw new NotSupportedException();
        public void UpdateSubject(Subject subject) => throw new NotSupportedException();
    }

    private sealed class TempDataProviderFake : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
