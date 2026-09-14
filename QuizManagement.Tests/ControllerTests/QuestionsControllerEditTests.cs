using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.Questions;
using Services;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class QuestionsControllerEditTests
{
    [Fact]
    public void Edit_RestoresOriginalAnswersAndShowsErrorWhenReferencedRemovalIsRejected()
    {
        var questionService = new QuestionServiceFake { Question = CreateQuestion() };
        var controller = BuildController(questionService);

        var result = controller.Edit(10, new QuestionFormViewModel
        {
            Id = 10,
            RowVersion = Convert.ToBase64String(questionService.Question!.RowVersion),
            Content = "Updated",
            QuestionType = 1,
            Answers =
            [
                new AnswerFormViewModel { Id = 1, Content = "A", IsCorrect = true },
                new AnswerFormViewModel { Id = 2, Content = "B" }
            ]
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<QuestionFormViewModel>(view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(model.Answers, answer => answer.Id == 3);
        Assert.Equal(1, questionService.TryUpdateCalls);
    }

    private static QuestionsController BuildController(QuestionServiceFake questionService)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "mentor"),
                new Claim(ClaimTypes.Role, AppRoles.Mentor)
            ], "Test"))
        };

        return new QuestionsController(new DeckServiceFake(), questionService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static Question CreateQuestion() => new()
    {
        Id = 10,
        Content = "Original",
        QuestionType = 1,
        RowVersion = [1, 2, 3, 4, 5, 6, 7, 8],
        Deck = new Deck
        {
            Id = 7,
            Name = "Deck",
            Subject = new Subject { Id = 5, Name = "Subject" }
        },
        Answers =
        [
            new Answer { Id = 1, Content = "A", IsCorrect = true },
            new Answer { Id = 2, Content = "B" },
            new Answer { Id = 3, Content = "Referenced" }
        ]
    };

    private sealed class QuestionServiceFake : IQuestionService
    {
        public Question? Question { get; init; }
        public int TryUpdateCalls { get; private set; }

        public Question? GetQuestionById(int id, string userId, bool allowAll = false) => Question;
        public QuestionUpdateResult TryUpdateQuestion(Question question)
        {
            TryUpdateCalls++;
            return QuestionUpdateResult.ReferencedAnswer;
        }

        public IEnumerable<Question> GetQuestionsByDeckForStudy(int deckId) => throw new NotSupportedException();
        public void AddQuestion(Question question) => throw new NotSupportedException();
        public void AddQuestions(IEnumerable<Question> questions) => throw new NotSupportedException();
        public void DeleteQuestion(Question question) => throw new NotSupportedException();
    }

    private sealed class DeckServiceFake : IDeckService
    {
        public IEnumerable<Deck> GetDecksBySubjectForStudy(int subjectId) => throw new NotSupportedException();
        public Deck? GetDeckForStudy(int id) => throw new NotSupportedException();
        public Deck? GetDeckById(int id, string userId, bool allowAll = false) => throw new NotSupportedException();
        public bool NameExists(int subjectId, string name, int? excludedId = null) => throw new NotSupportedException();
        public bool TryAddDeck(Deck deck) => throw new NotSupportedException();
        public bool TryUpdateDeck(Deck deck) => throw new NotSupportedException();
        public void DeleteDeck(Deck deck) => throw new NotSupportedException();
    }
}
