using Xunit;

namespace QuizManagement.Tests.ViewTests;

public class QuizErrorFeedbackViewTests
{
    [Theory]
    [InlineData("Questions", "Index.cshtml")]
    [InlineData("Quiz", "Config.cshtml")]
    public void RedirectDestination_RendersEncodedTempDataError(string folder, string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "QuizManagement", "Views", folder, fileName));
        var razor = File.ReadAllText(path);

        Assert.Contains("TempData[\"ErrorMessage\"] is string errorMessage", razor);
        Assert.Contains("@errorMessage", razor);
        Assert.Contains("alert alert-danger", razor);
        Assert.DoesNotContain("Html.Raw(errorMessage)", razor);
    }
}
