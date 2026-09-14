using System.Text.Json;
using BusinessObjects;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.ServiceTests;

public class QuizAttemptServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StartQuizAttempt_SnapshotsServerTimeLimitAndDistinctQuestions()
    {
        var repository = new QuizRepositoryFake();
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        var attempt = service.StartQuizAttempt(7, "user-a", [3, 3, 2], 30);

        Assert.NotEqual(Guid.Empty, attempt.Id);
        Assert.Equal("user-a", attempt.UserId);
        Assert.Equal(7, attempt.DeckId);
        Assert.Equal(30, attempt.TimeLimitMinutes);
        Assert.Equal(Now, attempt.StartedAtUtc);
        Assert.Equal(Now.AddMinutes(30), attempt.ExpiresAtUtc);
        Assert.Equal([3, 2], JsonSerializer.Deserialize<List<int>>(attempt.QuestionIdsJson));
        Assert.Same(attempt, repository.AddedAttempt);
    }

    [Fact]
    public void StartQuizAttempt_UsesNullExpiryForUnlimitedDeck()
    {
        var service = new QuizService(new QuizRepositoryFake(), new FixedTimeProvider(Now));

        var attempt = service.StartQuizAttempt(7, "user-a", [1], 0);

        Assert.Equal(0, attempt.TimeLimitMinutes);
        Assert.Null(attempt.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(181)]
    public void StartQuizAttempt_RejectsInvalidTimeLimitWithoutInsert(int minutes)
    {
        var repository = new QuizRepositoryFake();
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.StartQuizAttempt(7, "user-a", [1], minutes));
        Assert.Null(repository.AddedAttempt);
    }

    [Fact]
    public void StartQuizAttempt_RejectsInvalidQuestionIdsWithoutInsert()
    {
        var repository = new QuizRepositoryFake();
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        Assert.Throws<ArgumentException>(() => service.StartQuizAttempt(7, "user-a", [], 10));
        Assert.Throws<ArgumentException>(() => service.StartQuizAttempt(7, "user-a", [0], 10));
        Assert.Throws<ArgumentException>(() =>
            service.StartQuizAttempt(7, "user-a", Enumerable.Range(1, 501).ToList(), 10));
        Assert.Null(repository.AddedAttempt);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(30)]
    public void GetValidQuizAttempt_AcceptsBeforeAndAtExactExpiry(int elapsedMinutes)
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        var repository = new QuizRepositoryFake { Attempt = attempt };
        var service = new QuizService(
            repository,
            new FixedTimeProvider(Now.AddMinutes(elapsedMinutes)));

        var result = service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId);

        Assert.NotNull(result);
        Assert.Equal([3, 2], result.QuestionIds);
        Assert.Equal(attempt.ExpiresAtUtc, result.ExpiresAtUtc);
        Assert.Equal((30 - elapsedMinutes) * 60, result.RemainingSeconds);
        Assert.Equal((attempt.Id, attempt.DeckId, attempt.UserId), repository.LastAttemptLookup);
    }

    [Fact]
    public void GetValidQuizAttempt_RejectsAfterStoredExpiry()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        var repository = new QuizRepositoryFake { Attempt = attempt };
        var service = new QuizService(repository, new FixedTimeProvider(Now.AddMinutes(31)));

        var result = service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId);

        Assert.Null(result);
    }

    [Fact]
    public void GetValidQuizAttempt_AcceptsUnlimitedAttempt()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 0);
        var repository = new QuizRepositoryFake { Attempt = attempt };
        var service = new QuizService(repository, new FixedTimeProvider(Now.AddYears(10)));

        var result = service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId);

        Assert.NotNull(result);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Null(result.RemainingSeconds);
    }

    [Fact]
    public void GetQuestionsForAttempt_PreservesSnapshotOrderAndExcludesOtherQuestions()
    {
        var repository = new QuizRepositoryFake
        {
            Questions =
            [
                new Question { Id = 2 },
                new Question { Id = 3 },
                new Question { Id = 4 }
            ]
        };
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        var result = service.GetQuestionsForAttempt(7, [3, 2]);

        Assert.Equal([3, 2], result.Select(question => question.Id));
    }

    [Fact]
    public void GetValidQuizAttempt_ScopesLookupByAttemptDeckAndUser()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        var repository = new QuizRepositoryFake { Attempt = attempt };
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId + 1, attempt.UserId));
        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, "other-user"));
        Assert.Null(service.GetValidQuizAttempt(Guid.NewGuid(), attempt.DeckId, attempt.UserId));
    }

    [Fact]
    public void GetValidQuizAttempt_RejectsCompletedAndCorruptAttempts()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        var repository = new QuizRepositoryFake { Attempt = attempt };
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        attempt.CompletedAtUtc = Now;
        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId));

        attempt.CompletedAtUtc = null;
        attempt.QuestionIdsJson = "not-json";
        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId));

        attempt.QuestionIdsJson = "[3,3]";
        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(181)]
    public void GetValidQuizAttempt_RejectsInvalidPersistedTimeLimit(int timeLimitMinutes)
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        attempt.TimeLimitMinutes = timeLimitMinutes;
        var service = new QuizService(
            new QuizRepositoryFake { Attempt = attempt },
            new FixedTimeProvider(Now));

        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId));
    }

    [Fact]
    public void GetValidQuizAttempt_RejectsExpiryThatDoesNotMatchStoredSnapshot()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        attempt.ExpiresAtUtc = Now.AddHours(8);
        var service = new QuizService(
            new QuizRepositoryFake { Attempt = attempt },
            new FixedTimeProvider(Now.AddMinutes(31)));

        Assert.Null(service.GetValidQuizAttempt(attempt.Id, attempt.DeckId, attempt.UserId));
    }

    [Fact]
    public void SubmitQuizAttempt_GradesOnlyPersistedQuestionsAndCompletesWithServerTime()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        var repository = new QuizRepositoryFake
        {
            Attempt = attempt,
            Questions =
            [
                CreateSingleChoiceQuestion(2, 21),
                CreateSingleChoiceQuestion(3, 31),
                CreateSingleChoiceQuestion(999, 9991)
            ]
        };
        var completedAt = Now.AddMinutes(5);
        var service = new QuizService(repository, new FixedTimeProvider(completedAt));

        var result = service.SubmitQuizAttempt(
            attempt.Id,
            attempt.DeckId,
            attempt.UserId,
            new Dictionary<int, List<int>>
            {
                [3] = [31],
                [999] = [9991]
            });

        Assert.NotNull(result);
        Assert.Equal(attempt.Id, result.QuizAttemptId);
        Assert.Equal(5, result.Score);
        Assert.Equal(50, result.Percentage);
        Assert.Equal(completedAt.UtcDateTime, result.CreatedAt);
        Assert.DoesNotContain(result.TestResultDetails, detail => detail.QuestionId == 999);
        var snapshot = JsonSerializer.Deserialize<QuizResultSnapshot>(result.ResultSnapshotJson!);
        Assert.NotNull(snapshot);
        Assert.Equal("Deck", snapshot.DeckName);
        Assert.Equal("Subject", snapshot.SubjectName);
        Assert.Equal([3, 2], snapshot.Questions.Select(question => question.QuestionId));
        Assert.True(snapshot.Questions[0].IsCorrect);
        Assert.True(snapshot.Questions[0].Answers.Single(answer => answer.AnswerId == 31).WasSelected);
        Assert.Equal(completedAt, repository.CompletedAtUtc);
        Assert.Equal(1, repository.CompleteCalls);
    }

    [Fact]
    public void SubmitQuizAttempt_ReturnsExistingHistoryForCompletedRetry()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        attempt.CompletedAtUtc = Now;
        var existing = new TestHistory { Id = 42, QuizAttemptId = attempt.Id };
        var repository = new QuizRepositoryFake
        {
            Attempt = attempt,
            ExistingHistory = existing
        };
        var service = new QuizService(repository, new FixedTimeProvider(Now));

        var result = service.SubmitQuizAttempt(
            attempt.Id,
            attempt.DeckId,
            attempt.UserId,
            new Dictionary<int, List<int>>());

        Assert.Same(existing, result);
        Assert.Equal(0, repository.CompleteCalls);
    }

    [Fact]
    public void SubmitQuizAttempt_RejectsExpiredAttemptWithoutCompletion()
    {
        var attempt = CreateAttempt(timeLimitMinutes: 30);
        var repository = new QuizRepositoryFake { Attempt = attempt };
        var service = new QuizService(repository, new FixedTimeProvider(Now.AddMinutes(31)));

        var result = service.SubmitQuizAttempt(
            attempt.Id,
            attempt.DeckId,
            attempt.UserId,
            new Dictionary<int, List<int>>());

        Assert.Null(result);
        Assert.Equal(0, repository.CompleteCalls);
    }

    private static Question CreateSingleChoiceQuestion(int questionId, int correctAnswerId)
    {
        return new Question
        {
            Id = questionId,
            Content = $"Question {questionId}",
            Explanation = $"Explanation {questionId}",
            QuestionType = 1,
            Deck = new Deck
            {
                Id = 7,
                Name = "Deck",
                Subject = new Subject { Name = "Subject" }
            },
            Answers =
            [
                new Answer { Id = correctAnswerId, Content = "Correct", IsCorrect = true },
                new Answer { Id = correctAnswerId + 1, Content = "Incorrect", IsCorrect = false }
            ]
        };
    }

    private static QuizAttempt CreateAttempt(int timeLimitMinutes)
    {
        return new QuizAttempt
        {
            Id = Guid.NewGuid(),
            UserId = "user-a",
            DeckId = 7,
            QuestionIdsJson = "[3,2]",
            TimeLimitMinutes = timeLimitMinutes,
            StartedAtUtc = Now,
            ExpiresAtUtc = timeLimitMinutes == 0 ? null : Now.AddMinutes(timeLimitMinutes)
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class QuizRepositoryFake : IQuizRepository
    {
        public QuizAttempt? AddedAttempt { get; private set; }
        public QuizAttempt? Attempt { get; init; }
        public List<Question> Questions { get; init; } = [];
        public (Guid AttemptId, int DeckId, string UserId)? LastAttemptLookup { get; private set; }
        public TestHistory? ExistingHistory { get; init; }
        public int CompleteCalls { get; private set; }
        public DateTimeOffset? CompletedAtUtc { get; private set; }

        public QuizAttempt AddQuizAttempt(QuizAttempt attempt)
        {
            AddedAttempt = attempt;
            return attempt;
        }

        public QuizAttempt? GetQuizAttempt(Guid attemptId, int deckId, string userId)
        {
            LastAttemptLookup = (attemptId, deckId, userId);
            return Attempt is not null
                && Attempt.Id == attemptId
                && Attempt.DeckId == deckId
                && Attempt.UserId == userId
                ? Attempt
                : null;
        }

        public List<Question> GetQuestionsForQuiz(int deckId) => Questions;
        public int GetQuestionCount(int deckId) => throw new NotSupportedException();
        public TestHistory? GetTestHistoryByQuizAttempt(Guid attemptId, int deckId, string userId)
            => ExistingHistory?.QuizAttemptId == attemptId ? ExistingHistory : null;
        public TestHistory? CompleteQuizAttempt(
            Guid attemptId,
            int deckId,
            string userId,
            DateTimeOffset completedAtUtc,
            TestHistory history)
        {
            CompleteCalls++;
            CompletedAtUtc = completedAtUtc;
            return history;
        }
        public TestHistory? GetTestHistoryById(int id, string userId) => throw new NotSupportedException();
        public Task<TestHistoryPage> GetTestHistoryPageAsync(string userId, int page, int pageSize) => throw new NotSupportedException();
        public Task<UserStatisticsReadModel> GetUserStatisticsAsync(string userId) => throw new NotSupportedException();
        public Task<List<LeaderboardEntryReadModel>> GetLeaderboardAsync(int deckId, int count) => throw new NotSupportedException();
        public Task<MentorStatisticsReadModel> GetMentorStatisticsAsync(string ownerUserId, bool isAdmin) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByUser(string userId) => throw new NotSupportedException();
        public List<TestHistory> GetRecentTestHistories(string userId, int count) => throw new NotSupportedException();
        public (int totalQuizzes, double averagePercentage, DateTime? lastQuizDate) GetQuizStatistics(string userId) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByDeck(int deckId) => throw new NotSupportedException();
        public List<TestHistory> GetTestHistoriesByContentOwner(string ownerUserId, bool isAdmin) => throw new NotSupportedException();
    }
}
