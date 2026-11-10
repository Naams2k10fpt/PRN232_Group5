using System.Data.Common;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Repositories;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class FlashcardReviewConcurrencyPostgresTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "PostgresIntegration")]
    public async Task ConcurrentFirstReviews_KeepOneProgressRowAndApplyEachIdOnce(bool replay)
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var setupOptions = CreateOptions(databaseName);
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        int questionId;

        using (var setupContext = new QuizManagementDbContext(setupOptions))
        {
            setupContext.Database.EnsureCreated();
            var question = new Question
            {
                Content = "Question",
                QuestionType = 1,
                Deck = new Deck
                {
                    Name = "Deck",
                    Subject = new Subject
                    {
                        Name = "Subject",
                        User = new User
                        {
                            Id = "user",
                            Username = "user",
                            Email = "user@test.local",
                            Role = AppRoles.User,
                            EmailConfirmed = true
                        }
                    }
                }
            };
            setupContext.Questions.Add(question);
            setupContext.SaveChanges();
            questionId = question.Id;
        }

        try
        {
            // Both workers read "no progress yet" and are held at their INSERT until the other one
            // arrives, so the second INSERT always hits the unique (UserId, QuestionId) index.
            using var barrier = new Barrier(2);
            var interceptor = new InsertBarrierInterceptor(barrier, "INSERT INTO \"FlashcardProgresses\"");
            var workerOptions = CreateOptions(databaseName, interceptor);

            var firstId = Guid.NewGuid();
            var secondId = replay ? firstId : Guid.NewGuid();
            FlashcardProgress Review(Guid reviewId)
            {
                using var context = new QuizManagementDbContext(workerOptions);
                return new QuestionRepository(context).ReviewFlashcard("user", questionId, true, now, reviewId);
            }

            var results = await Task.WhenAll(Task.Run(() => Review(firstId)), Task.Run(() => Review(secondId)));

            Assert.Equal(2, interceptor.InsertCount);
            Assert.All(results, progress => Assert.Equal(questionId, progress.QuestionId));
            using var verificationContext = new QuizManagementDbContext(setupOptions);
            var saved = Assert.Single(verificationContext.FlashcardProgresses);
            Assert.Equal("user", saved.UserId);
            var expectedReviews = replay ? 1 : 2;
            Assert.Equal(expectedReviews, saved.Repetition);
            Assert.Equal(expectedReviews, verificationContext.FlashcardReviewReceipts.Count());

            // Replay the earlier id after a later successful review.
            using var retryContext = new QuizManagementDbContext(setupOptions);
            var retried = new QuestionRepository(retryContext)
                .ReviewFlashcard("user", questionId, true, now.AddDays(1), firstId);
            Assert.Equal(saved.NextReviewAtUtc, retried.NextReviewAtUtc);
            Assert.Equal(expectedReviews, retried.Repetition);
            Assert.Equal(results.Max(progress => progress.Id), results.Min(progress => progress.Id));
            Assert.Equal(saved.Id, results[0].Id);
        }
        finally
        {
            using var cleanupContext = new QuizManagementDbContext(setupOptions);
            cleanupContext.Database.EnsureDeleted();
        }
    }

    private static DbContextOptions<QuizManagementDbContext> CreateOptions(
        string databaseName,
        DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName));
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private sealed class InsertBarrierInterceptor(Barrier barrier, string commandMarker)
        : DbCommandInterceptor
    {
        private int _insertCount;

        public int InsertCount => Volatile.Read(ref _insertCount);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains(commandMarker, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _insertCount);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Concurrent inserts did not reach the SQL barrier.");
            }

            return result;
        }
    }
}
