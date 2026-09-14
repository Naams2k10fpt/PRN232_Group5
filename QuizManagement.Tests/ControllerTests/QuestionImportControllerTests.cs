using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.Import;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class QuestionImportControllerTests
{
    [Fact]
    public void Commit_WhenAnyPostedRowIsInvalid_WritesNothing()
    {
        var questionService = new QuestionServiceFake();
        var controller = BuildController(questionService);
        var model = CreateModel();
        model.ValidRows.Add(new QuestionImportRowViewModel
        {
            RowNumber = 2,
            Content = "Invalid",
            QuestionType = 1,
            Answers = [new() { Content = "Only answer", IsCorrect = true }]
        });

        var result = controller.Commit(model);

        var view = Assert.IsType<ViewResult>(result);
        var preview = Assert.IsType<QuestionImportPreviewViewModel>(view.Model);
        Assert.Single(preview.Errors);
        Assert.Equal(0, questionService.AddQuestionsCalls);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public void Commit_WhenAllRowsAreValid_WritesOneBatch()
    {
        var questionService = new QuestionServiceFake();
        var controller = BuildController(questionService);
        var model = CreateModel();
        model.ValidRows.Add(CreateValidRow(2));

        var result = controller.Commit(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Questions", redirect.ControllerName);
        Assert.Equal(1, questionService.AddQuestionsCalls);
        Assert.Equal(2, questionService.LastBatch.Count);
    }

    [Fact]
    public void Preview_WhenParserFails_ReturnsGenericErrorWithoutParserDetail()
    {
        var questionService = new QuestionServiceFake();
        var controller = BuildController(questionService, new FailingImportService());

        var result = controller.Preview(new QuestionImportInputViewModel
        {
            DeckId = 7,
            RawText = "content"
        });

        Assert.IsType<ViewResult>(result);
        var messages = controller.ModelState
            .SelectMany(entry => entry.Value!.Errors)
            .Select(error => error.ErrorMessage)
            .ToList();
        Assert.Contains(messages, message => message.Contains("kiểm tra định dạng", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(messages, message => message.Contains("secret parser detail", StringComparison.OrdinalIgnoreCase));
    }

    private static QuestionImportController BuildController(
        QuestionServiceFake questionService,
        IQuestionImportService? importService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "mentor"),
                new Claim(ClaimTypes.Name, "mentor"),
                new Claim(ClaimTypes.Role, AppRoles.Mentor)
            ], "Test"))
        };

        return new QuestionImportController(
            new DeckServiceFake(),
            questionService,
            importService ?? new QuestionImportService(),
            NullLogger<QuestionImportController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TempDataProviderFake())
        };
    }

    private static QuestionImportPreviewViewModel CreateModel() => new()
    {
        DeckId = 7,
        SourceName = "Test",
        ValidRows = [CreateValidRow(1)]
    };

    private static QuestionImportRowViewModel CreateValidRow(int rowNumber) => new()
    {
        RowNumber = rowNumber,
        Content = $"Question {rowNumber}",
        QuestionType = 1,
        Answers =
        [
            new() { Content = "Correct", IsCorrect = true },
            new() { Content = "Wrong" }
        ]
    };

    private sealed class QuestionServiceFake : IQuestionService
    {
        public int AddQuestionsCalls { get; private set; }
        public List<Question> LastBatch { get; private set; } = [];

        public void AddQuestions(IEnumerable<Question> questions)
        {
            AddQuestionsCalls++;
            LastBatch = questions.ToList();
        }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId) => throw new NotSupportedException();
        public Question? GetQuestionById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public void AddQuestion(Question question) => throw new NotSupportedException();
        public QuestionUpdateResult TryUpdateQuestion(Question question) => throw new NotSupportedException();
        public void DeleteQuestion(Question question) => throw new NotSupportedException();
    }

    private sealed class DeckServiceFake : IDeckService
    {
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => id == 7
            ? new Deck
            {
                Id = 7,
                Name = "Deck",
                Subject = new Subject { Name = "Subject", UserId = userId }
            }
            : null;

        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckForStudy(int id) => throw new NotSupportedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotSupportedException();
        public bool TryAddDeck(Deck deck) => throw new NotSupportedException();
        public bool TryUpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }

    private sealed class FailingImportService : IQuestionImportService
    {
        public QuestionImportPreview ParseText(string text)
            => throw new InvalidDataException("secret parser detail");

        public QuestionImportPreview ParseExcel(Stream stream) => throw new NotSupportedException();
        public QuestionImportPreview ValidateRows(IEnumerable<QuestionImportRow> rows) => throw new NotSupportedException();
        public string GetTextTemplate() => string.Empty;
    }

    private sealed class TempDataProviderFake : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
