using System.Data.Common;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuestionReportDedupSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentPendingSubmissions_CreateOneReportAndOneFriendlyConflict()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var setupOptions = CreateOptions(databaseName);
        int questionId;
        using (var setup = new QuizManagementDbContext(setupOptions))
        {
            setup.Database.EnsureCreated();
            questionId = SeedQuestion(setup);
        }

        try
        {
            using var barrier = new Barrier(2);
            var interceptor = new InsertBarrierInterceptor(barrier);
            var workerOptions = CreateOptions(databaseName, interceptor);

            QuestionReportSubmission Submit()
            {
                using var context = new QuizManagementDbContext(workerOptions);
                return new QuestionReportService(new QuestionReportRepository(context))
                    .Submit(questionId, "reporter", "WrongAnswer", null);
            }

            var results = await Task.WhenAll(
                Task.Run(Submit),
                Task.Run(Submit));

            Assert.Equal(1, results.Count(result => result == QuestionReportSubmission.Submitted));
            Assert.Equal(1, results.Count(result => result == QuestionReportSubmission.AlreadyPending));
            Assert.Equal(2, interceptor.InsertCount);
            using var verification = new QuizManagementDbContext(setupOptions);
            Assert.Single(verification.QuestionReports.Where(report => !report.IsResolved));
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(setupOptions);
            cleanup.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void ResolvedReport_DoesNotBlockANewPendingReport()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = CreateOptions(databaseName);
        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var questionId = SeedQuestion(context);
            var service = new QuestionReportService(new QuestionReportRepository(context));

            Assert.Equal(
                QuestionReportSubmission.Submitted,
                service.Submit(questionId, "reporter", "WrongAnswer", null));
            var first = context.QuestionReports.Single();
            first.IsResolved = true;
            context.SaveChanges();

            Assert.Equal(
                QuestionReportSubmission.Submitted,
                service.Submit(questionId, "reporter", "Other", "Still an issue"));
            Assert.Equal(2, context.QuestionReports.Count());
            Assert.Single(context.QuestionReports.Where(report => !report.IsResolved));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static int SeedQuestion(QuizManagementDbContext context)
    {
        context.Users.AddRange(
            CreateUser("mentor", AppRoles.Mentor),
            CreateUser("reporter", AppRoles.User));
        var subject = new Subject { UserId = "mentor", Name = "Subject" };
        context.Subjects.Add(subject);
        context.SaveChanges();
        var deck = new Deck { SubjectId = subject.Id, Name = "Deck" };
        context.Decks.Add(deck);
        context.SaveChanges();
        var question = new Question
        {
            DeckId = deck.Id,
            Content = "Question",
            QuestionType = 1
        };
        context.Questions.Add(question);
        context.SaveChanges();
        return question.Id;
    }

    private static User CreateUser(string id, string role) => new()
    {
        Id = id,
        Username = id,
        Email = $"{id}@test.local",
        Role = role,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static DbContextOptions<QuizManagementDbContext> CreateOptions(
        string databaseName,
        DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName));
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private sealed class InsertBarrierInterceptor(Barrier barrier) : DbCommandInterceptor
    {
        private int _insertCount;

        public int InsertCount => Volatile.Read(ref _insertCount);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("INSERT INTO [QuestionReports]", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _insertCount);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Concurrent report inserts did not reach the SQL barrier.");
            }

            return result;
        }
    }
}
