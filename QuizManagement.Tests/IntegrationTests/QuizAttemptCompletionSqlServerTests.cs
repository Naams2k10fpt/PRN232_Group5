using System.Data.Common;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizManagement.ViewModels.Quiz;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuizAttemptCompletionSqlServerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void SubmitQuizAttempt_RetryReturnsSameSingleHistory()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = CreateOptions(databaseName);
        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var (userId, deckId, questionId, correctAnswerId) = SeedQuiz(context);
            var repository = new QuizRepository(context);
            var attempt = new QuizService(repository, new FixedTimeProvider(Now))
                .StartQuizAttempt(deckId, userId, [questionId], 30);
            var submitService = new QuizService(
                repository,
                new FixedTimeProvider(Now.AddMinutes(5)));
            var answers = new Dictionary<int, List<int>> { [questionId] = [correctAnswerId] };

            var first = submitService.SubmitQuizAttempt(attempt.Id, deckId, userId, answers);
            var retry = submitService.SubmitQuizAttempt(attempt.Id, deckId, userId, answers);

            Assert.NotNull(first);
            Assert.NotNull(retry);
            Assert.Equal(first.Id, retry.Id);
            context.ChangeTracker.Clear();
            Assert.Equal(1, context.TestHistories.Count(history => history.QuizAttemptId == attempt.Id));
            Assert.Equal(attempt.Id, context.TestHistories.Single().QuizAttemptId);
            Assert.Equal(Now.AddMinutes(5), context.QuizAttempts.Single().CompletedAtUtc);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void QuizResultSnapshot_RemainsImmutableAfterLiveContentChangesAndSoftDelete()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = CreateOptions(databaseName);
        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var (userId, deckId, questionId, correctAnswerId) = SeedQuiz(context);
            var repository = new QuizRepository(context);
            var attempt = new QuizService(repository, new FixedTimeProvider(Now))
                .StartQuizAttempt(deckId, userId, [questionId], 30);
            var history = new QuizService(repository, new FixedTimeProvider(Now.AddMinutes(5)))
                .SubmitQuizAttempt(
                    attempt.Id,
                    deckId,
                    userId,
                    new Dictionary<int, List<int>> { [questionId] = [correctAnswerId] });
            Assert.NotNull(history);

            var question = context.Questions
                .IgnoreQueryFilters()
                .Include(item => item.Answers)
                .Include(item => item.Deck)
                    .ThenInclude(deck => deck.Subject)
                .Single(item => item.Id == questionId);
            question.Content = "Changed question";
            question.IsDeleted = true;
            question.Deck.Name = "Changed deck";
            question.Deck.IsDeleted = true;
            question.Deck.Subject.Name = "Changed subject";
            question.Deck.Subject.IsDeleted = true;
            foreach (var answer in question.Answers)
            {
                answer.Content = $"Changed {answer.Id}";
                answer.IsCorrect = !answer.IsCorrect;
            }
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var persisted = new QuizRepository(context).GetTestHistoryById(history.Id, userId);
            var model = QuizResultViewModel.FromHistory(Assert.IsType<TestHistory>(persisted));

            Assert.Equal("Deck", model.DeckName);
            Assert.Equal("Subject", model.SubjectName);
            var resultQuestion = Assert.Single(model.Questions);
            Assert.Equal("Question", resultQuestion.Content);
            var selected = Assert.Single(resultQuestion.Answers, answer => answer.WasSelected);
            Assert.Equal("Correct", selected.Content);
            Assert.True(selected.IsCorrectAnswer);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void CompleteQuizAttempt_RejectsForeignAndExpiredAndRollsBackFailedInsert()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = CreateOptions(databaseName);
        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var (userId, deckId, questionId, _) = SeedQuiz(context);
            var repository = new QuizRepository(context);
            var attempt = new QuizService(repository, new FixedTimeProvider(Now))
                .StartQuizAttempt(deckId, userId, [questionId], 1);

            Assert.Null(repository.CompleteQuizAttempt(
                attempt.Id,
                deckId,
                "foreign-user",
                Now.AddSeconds(10),
                CreateHistory("foreign-user", deckId)));
            Assert.Null(repository.CompleteQuizAttempt(
                attempt.Id,
                deckId,
                userId,
                Now.AddMinutes(2),
                CreateHistory(userId, deckId)));

            Assert.Throws<DbUpdateException>(() => repository.CompleteQuizAttempt(
                attempt.Id,
                deckId,
                userId,
                Now.AddSeconds(30),
                CreateHistory("missing-user", deckId)));

            context.ChangeTracker.Clear();
            Assert.Null(context.QuizAttempts.Single().CompletedAtUtc);
            Assert.Empty(context.TestHistories);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SubmitQuizAttempt_TwoConcurrentConnectionsCreateOneHistory()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var setupOptions = CreateOptions(databaseName);
        Guid attemptId;
        int deckId;
        int questionId;
        int correctAnswerId;
        string userId;
        using (var setupContext = new QuizManagementDbContext(setupOptions))
        {
            setupContext.Database.EnsureCreated();
            (userId, deckId, questionId, correctAnswerId) = SeedQuiz(setupContext);
            attemptId = new QuizService(
                    new QuizRepository(setupContext),
                    new FixedTimeProvider(Now))
                .StartQuizAttempt(deckId, userId, [questionId], 30)
                .Id;
        }

        try
        {
            using var barrier = new Barrier(2);
            var interceptor = new CompletionUpdateBarrierInterceptor(barrier);
            var workerOptions = CreateOptions(databaseName, interceptor);

            TestHistory? Submit()
            {
                using var context = new QuizManagementDbContext(workerOptions);
                var service = new QuizService(
                    new QuizRepository(context),
                    new FixedTimeProvider(Now.AddMinutes(5)));
                return service.SubmitQuizAttempt(
                    attemptId,
                    deckId,
                    userId,
                    new Dictionary<int, List<int>> { [questionId] = [correctAnswerId] });
            }

            var results = await Task.WhenAll(Task.Run(Submit), Task.Run(Submit));

            Assert.NotNull(results[0]);
            Assert.NotNull(results[1]);
            Assert.Equal(results[0]!.Id, results[1]!.Id);
            Assert.Equal(2, interceptor.CompletionUpdateCount);
            using var verificationContext = new QuizManagementDbContext(setupOptions);
            Assert.Equal(1, verificationContext.TestHistories.Count(
                history => history.QuizAttemptId == attemptId));
            Assert.Equal(Now.AddMinutes(5), verificationContext.QuizAttempts
                .Single(attempt => attempt.Id == attemptId)
                .CompletedAtUtc);
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
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName));
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private static (string UserId, int DeckId, int QuestionId, int CorrectAnswerId) SeedQuiz(
        QuizManagementDbContext context)
    {
        var user = new User
        {
            Id = "user-a",
            Username = "user-a",
            Email = "user-a@test.local",
            Role = AppRoles.User,
            CreatedAt = DateTime.UtcNow
        };
        var question = new Question
        {
            Content = "Question",
            QuestionType = 1,
            CreatedAt = DateTime.UtcNow,
            Answers =
            [
                new Answer { Content = "Correct", IsCorrect = true },
                new Answer { Content = "Incorrect", IsCorrect = false }
            ],
            Deck = new Deck
            {
                Name = "Deck",
                TimeLimitMinutes = 30,
                CreatedAt = DateTime.UtcNow,
                Subject = new Subject
                {
                    Name = "Subject",
                    User = user,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };
        context.Questions.Add(question);
        context.SaveChanges();
        return (user.Id, question.DeckId, question.Id, question.Answers.First(answer => answer.IsCorrect).Id);
    }

    private static TestHistory CreateHistory(string userId, int deckId)
    {
        return new TestHistory
        {
            UserId = userId,
            DeckId = deckId,
            CreatedAt = Now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CompletionUpdateBarrierInterceptor(Barrier barrier) : DbCommandInterceptor
    {
        private int _completionUpdateCount;

        public int CompletionUpdateCount => Volatile.Read(ref _completionUpdateCount);

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("CompletedAtUtc", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _completionUpdateCount);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Concurrent completion commands did not reach the SQL barrier.");
            }

            return result;
        }
    }
}
