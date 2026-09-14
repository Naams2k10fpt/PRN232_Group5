using System.Reflection;
using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.QuestionReports;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class QuestionReportsControllerTests
{
    [Fact]
    public void Resolve_ReturnsNotFound_WhenMentorCannotSeeReportInActorScope()
    {
        var service = new ReportServiceFake { Result = QuestionReportResolution.NotFound };
        var controller = BuildController(service, AppRoles.Mentor, "mentor-a");

        var result = controller.Resolve(42);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal((42, "mentor-a", false), service.LastResolve);
    }

    [Fact]
    public void Resolve_AllowsAdminScope_AndRedirectsAfterSuccess()
    {
        var service = new ReportServiceFake { Result = QuestionReportResolution.Resolved };
        var controller = BuildController(service, AppRoles.Admin, "admin-a");

        var result = controller.Resolve(42);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal((42, "admin-a", true), service.LastResolve);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public void Resolve_ReportsAlreadyResolvedWithoutWritingAgain()
    {
        var service = new ReportServiceFake { Result = QuestionReportResolution.AlreadyResolved };
        var controller = BuildController(service, AppRoles.Mentor, "mentor-a");

        var result = controller.Resolve(42);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public void Resolve_RequiresManageContentPolicy()
    {
        var action = typeof(QuestionReportsController).GetMethod(nameof(QuestionReportsController.Resolve));

        var policies = action!.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy);

        Assert.Contains("ManageContent", policies);
    }

    [Fact]
    public void CreatePost_ReturnsNotFound_WhenActiveQuestionCannotBeReloaded()
    {
        var reportService = new ReportServiceFake();
        var questionService = new QuestionServiceFake();
        var controller = BuildController(
            reportService,
            AppRoles.User,
            "user-a",
            questionService);

        var result = controller.Create(new SubmitReportViewModel
        {
            QuestionId = 404,
            Reason = "WrongAnswer"
        });

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, reportService.SubmitCalls);
    }

    [Fact]
    public void CreatePost_RestoresServerQuestionContent_WhenValidationFails()
    {
        var questionService = new QuestionServiceFake
        {
            Question = new Question { Id = 7, Content = "Server content" }
        };
        var controller = BuildController(
            new ReportServiceFake(),
            AppRoles.User,
            "user-a",
            questionService);
        controller.ModelState.AddModelError(nameof(SubmitReportViewModel.Reason), "invalid");

        var result = controller.Create(new SubmitReportViewModel
        {
            QuestionId = 7,
            QuestionContent = "Tampered content",
            Reason = "invalid"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SubmitReportViewModel>(view.Model);
        Assert.Equal("Server content", model.QuestionContent);
    }

    [Fact]
    public void CreatePost_ReturnsFriendlyValidation_WhenReportIsAlreadyPending()
    {
        var reportService = new ReportServiceFake
        {
            SubmissionResult = QuestionReportSubmission.AlreadyPending
        };
        var questionService = new QuestionServiceFake
        {
            Question = new Question { Id = 7, Content = "Server content" }
        };
        var controller = BuildController(
            reportService,
            AppRoles.User,
            "user-a",
            questionService);

        var result = controller.Create(new SubmitReportViewModel
        {
            QuestionId = 7,
            Reason = "WrongAnswer"
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, reportService.SubmitCalls);
        Assert.False(controller.TempData.ContainsKey("SuccessMessage"));
    }

    private static QuestionReportsController BuildController(
        ReportServiceFake service,
        string role,
        string userId,
        QuestionServiceFake? questionService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            ], "Test"))
        };

        return new QuestionReportsController(service, questionService ?? new QuestionServiceFake())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TempDataProviderFake())
        };
    }

    private sealed class ReportServiceFake : IQuestionReportService
    {
        public QuestionReportResolution Result { get; init; }
        public QuestionReportSubmission SubmissionResult { get; init; }
            = QuestionReportSubmission.Submitted;
        public (int Id, string ActorUserId, bool AllowAll) LastResolve { get; private set; }
        public int SubmitCalls { get; private set; }

        public QuestionReportResolution Resolve(int reportId, string actorUserId, bool allowAll)
        {
            LastResolve = (reportId, actorUserId, allowAll);
            return Result;
        }

        public QuestionReportSubmission Submit(int questionId, string userId, string reason, string? note)
        {
            SubmitCalls++;
            return SubmissionResult;
        }
        public List<QuestionReport> GetAllReports() => throw new NotSupportedException();
        public List<QuestionReport> GetReportsByContentOwner(string ownerUserId) => throw new NotSupportedException();
        public Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
            string ownerUserId,
            bool allowAll,
            int page,
            int pageSize) => throw new NotSupportedException();
    }

    private sealed class QuestionServiceFake : IQuestionService
    {
        public Question? Question { get; init; }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId) => throw new NotSupportedException();
        public Question? GetQuestionById(int id, string userId, bool allowAll = false) => Question;
        public void AddQuestion(Question question) => throw new NotSupportedException();
        public void AddQuestions(IEnumerable<Question> questions) => throw new NotSupportedException();
        public QuestionUpdateResult TryUpdateQuestion(Question question) => throw new NotSupportedException();
        public void DeleteQuestion(Question question) => throw new NotSupportedException();
    }

    private sealed class TempDataProviderFake : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
