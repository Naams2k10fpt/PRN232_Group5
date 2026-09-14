using BusinessObjects;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class FlashcardProgressTests
{
    [Fact]
    public void Review_UsesPersistentBinarySpacedSchedule()
    {
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var progress = new FlashcardProgress();

        progress.Review(true, now);
        Assert.Equal(1, progress.Repetition);
        Assert.Equal(now.AddDays(1), progress.NextReviewAtUtc);

        progress.Review(true, now.AddDays(1));
        Assert.Equal(2, progress.Repetition);
        Assert.Equal(now.AddDays(4), progress.NextReviewAtUtc);

        progress.Review(false, now.AddDays(4));
        Assert.Equal(0, progress.Repetition);
        Assert.Equal(now.AddDays(4).AddMinutes(10), progress.NextReviewAtUtc);
        Assert.InRange(progress.EaseFactor, 2.49, 2.51);
    }
}
