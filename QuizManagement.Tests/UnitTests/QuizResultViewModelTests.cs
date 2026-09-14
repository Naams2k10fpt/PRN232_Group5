using BusinessObjects;
using QuizManagement.ViewModels.Quiz;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class QuizResultViewModelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-json")]
    public void FromHistory_FallsBackToLiveNavigationForLegacyOrMalformedSnapshot(string? snapshotJson)
    {
        var answer = new Answer { Id = 3, Content = "Legacy answer", IsCorrect = true };
        var question = new Question
        {
            Id = 2,
            Content = "Legacy question",
            QuestionType = 1,
            Answers = [answer]
        };
        var history = new TestHistory
        {
            Id = 1,
            ResultSnapshotJson = snapshotJson,
            Deck = new Deck
            {
                Name = "Legacy deck",
                Subject = new Subject { Name = "Legacy subject" }
            },
            TestResultDetails =
            [
                new TestResultDetail
                {
                    QuestionId = question.Id,
                    Question = question,
                    SelectedAnswerId = answer.Id,
                    SelectedAnswer = answer,
                    IsCorrect = true
                }
            ]
        };

        var result = QuizResultViewModel.FromHistory(history);

        Assert.Equal("Legacy deck", result.DeckName);
        Assert.Equal("Legacy question", Assert.Single(result.Questions).Content);
        Assert.True(Assert.Single(result.Questions[0].Answers).WasSelected);
    }
}
