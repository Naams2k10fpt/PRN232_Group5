using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class QuizAttemptPersistenceSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void StartQuizAttempt_PersistsSnapshotInSqlServer()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var user = new User
            {
                Id = "user-a",
                Username = "user-a",
                Email = "user-a@test.local",
                Role = AppRoles.User,
                CreatedAt = DateTime.UtcNow
            };
            var deck = new Deck
            {
                Name = "Deck",
                CreatedAt = DateTime.UtcNow,
                Subject = new Subject
                {
                    Name = "Subject",
                    User = user,
                    CreatedAt = DateTime.UtcNow
                }
            };
            context.Decks.Add(deck);
            context.SaveChanges();
            var now = new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero);
            var service = new QuizService(new QuizRepository(context), new FixedTimeProvider(now));

            var attempt = service.StartQuizAttempt(deck.Id, user.Id, [11, 12], 15);
            context.ChangeTracker.Clear();

            var persisted = context.QuizAttempts.Single(item => item.Id == attempt.Id);
            Assert.Equal(user.Id, persisted.UserId);
            Assert.Equal(deck.Id, persisted.DeckId);
            Assert.Equal(15, persisted.TimeLimitMinutes);
            Assert.Equal(now, persisted.StartedAtUtc);
            Assert.Equal(now.AddMinutes(15), persisted.ExpiresAtUtc);
            Assert.Null(persisted.CompletedAtUtc);

            var repository = new QuizRepository(context);
            Assert.NotNull(repository.GetQuizAttempt(attempt.Id, deck.Id, user.Id));
            Assert.Null(repository.GetQuizAttempt(attempt.Id, deck.Id + 1, user.Id));
            Assert.Null(repository.GetQuizAttempt(attempt.Id, deck.Id, "other-user"));
            Assert.Null(repository.GetQuizAttempt(Guid.NewGuid(), deck.Id, user.Id));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
