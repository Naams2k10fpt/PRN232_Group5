using System.ComponentModel.DataAnnotations;
using QuizManagement.ViewModels.QuestionReports;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class QuestionReportValidationTests
{
    [Theory]
    [InlineData("WrongAnswer", true)]
    [InlineData("UnclearQuestion", true)]
    [InlineData("DuplicateQuestion", true)]
    [InlineData("Other", true)]
    [InlineData("ArbitraryReason", false)]
    [InlineData("", false)]
    public void Reason_UsesTheSupportedWhitelist(string reason, bool expectedValid)
    {
        var model = new SubmitReportViewModel { QuestionId = 1, Reason = reason };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public void Reason_RejectsMoreThanOneHundredCharacters()
    {
        var model = new SubmitReportViewModel
        {
            QuestionId = 1,
            Reason = new string('x', 101)
        };

        Assert.False(Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            [],
            validateAllProperties: true));
    }
}
