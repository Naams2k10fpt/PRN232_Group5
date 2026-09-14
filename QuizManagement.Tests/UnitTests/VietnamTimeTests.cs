using QuizManagement.Helpers;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class VietnamTimeTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void FromUtc_UsesTheDocumentedUtcPlusSevenZone(DateTimeKind kind)
    {
        var instant = DateTime.SpecifyKind(new DateTime(2026, 7, 16, 18, 30, 0), kind);

        var displayed = VietnamTime.FromUtc(instant);

        Assert.Equal(new DateTime(2026, 7, 17, 1, 30, 0), displayed);
        Assert.Equal(DateTimeKind.Unspecified, displayed.Kind);
    }
}
