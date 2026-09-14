using BusinessObjects;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.ServiceTests;

public class QuestionReportServiceTests
{
    [Fact]
    public void Resolve_UsesActorScopedQuery_AndMarksReportResolved()
    {
        var report = new QuestionReport();
        var repository = new ReportRepositoryFake { ScopedReport = report };
        var service = new QuestionReportService(repository);

        var result = service.Resolve(42, "mentor-a", allowAll: false);

        Assert.Equal(QuestionReportResolution.Resolved, result);
        Assert.Equal((42, "mentor-a", false), repository.LastScope);
        Assert.True(report.IsResolved);
        Assert.Equal(1, repository.ResolveCalls);
    }

    [Fact]
    public void Resolve_DoesNotMutate_WhenActorScopedQueryFindsNothing()
    {
        var repository = new ReportRepositoryFake();
        var service = new QuestionReportService(repository);

        var result = service.Resolve(42, "mentor-a", allowAll: false);

        Assert.Equal(QuestionReportResolution.NotFound, result);
        Assert.Equal(0, repository.ResolveCalls);
    }

    [Fact]
    public void Resolve_IsSafeNoOp_WhenReportWasAlreadyResolved()
    {
        var repository = new ReportRepositoryFake
        {
            ScopedReport = new QuestionReport { IsResolved = true }
        };
        var service = new QuestionReportService(repository);

        var result = service.Resolve(42, "admin", allowAll: true);

        Assert.Equal(QuestionReportResolution.AlreadyResolved, result);
        Assert.Equal((42, "admin", true), repository.LastScope);
        Assert.Equal(0, repository.ResolveCalls);
    }

    private sealed class ReportRepositoryFake : IQuestionReportRepository
    {
        public QuestionReport? ScopedReport { get; init; }
        public (int Id, string ActorUserId, bool AllowAll) LastScope { get; private set; }
        public int ResolveCalls { get; private set; }

        public QuestionReport? GetForResolution(int id, string actorUserId, bool allowAll)
        {
            LastScope = (id, actorUserId, allowAll);
            return ScopedReport;
        }

        public void Resolve(QuestionReport report)
        {
            report.IsResolved = true;
            ResolveCalls++;
        }

        public void Create(QuestionReport report) => throw new NotSupportedException();
        public List<QuestionReport> GetAll() => throw new NotSupportedException();
        public List<QuestionReport> GetByContentOwner(string ownerUserId) => throw new NotSupportedException();
        public Task<(List<QuestionReport> Reports, int TotalCount)> GetPageAsync(
            string ownerUserId,
            bool allowAll,
            int page,
            int pageSize) => throw new NotSupportedException();
        public bool HasPendingReport(int questionId, string userId) => throw new NotSupportedException();
    }
}
