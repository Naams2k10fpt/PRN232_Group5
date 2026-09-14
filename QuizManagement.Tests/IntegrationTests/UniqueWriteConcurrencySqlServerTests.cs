using System.Data.Common;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class UniqueWriteConcurrencySqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentNormalizedUserWrites_ReturnOneConflict()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var setupOptions = CreateOptions(databaseName);
        using (var setupContext = new QuizManagementDbContext(setupOptions))
            setupContext.Database.EnsureCreated();

        try
        {
            using var barrier = new Barrier(2);
            var interceptor = new InsertBarrierInterceptor(barrier, "INSERT INTO [Users]");
            var workerOptions = CreateOptions(databaseName, interceptor);

            bool Create(User user)
            {
                using var context = new QuizManagementDbContext(workerOptions);
                return new UserService(new UserRepository(context)).TryCreateUser(user);
            }

            var results = await Task.WhenAll(
                Task.Run(() => Create(CreateUser("user-a", "race@test.local"))),
                Task.Run(() => Create(CreateUser("user-b", " RACE@test.local "))));

            Assert.Equal(1, results.Count(result => result));
            Assert.Equal(1, results.Count(result => !result));
            Assert.Equal(2, interceptor.InsertCount);
            using var verificationContext = new QuizManagementDbContext(setupOptions);
            Assert.Single(verificationContext.Users);
        }
        finally
        {
            using var cleanupContext = new QuizManagementDbContext(setupOptions);
            cleanupContext.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentNormalizedDeckWrites_ReturnOneConflict()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var setupOptions = CreateOptions(databaseName);
        int subjectId;
        using (var setupContext = new QuizManagementDbContext(setupOptions))
        {
            setupContext.Database.EnsureCreated();
            setupContext.Users.Add(CreateUser("mentor", "mentor@test.local"));
            var subject = new Subject
            {
                UserId = "mentor",
                Name = "Subject"
            };
            setupContext.Subjects.Add(subject);
            setupContext.SaveChanges();
            subjectId = subject.Id;
        }

        try
        {
            using var barrier = new Barrier(2);
            var interceptor = new InsertBarrierInterceptor(barrier, "INSERT INTO [Decks]");
            var workerOptions = CreateOptions(databaseName, interceptor);

            bool Create(string name)
            {
                using var context = new QuizManagementDbContext(workerOptions);
                return new DeckService(new DeckRepository(context)).TryAddDeck(new Deck
                {
                    SubjectId = subjectId,
                    Name = name
                });
            }

            var results = await Task.WhenAll(
                Task.Run(() => Create("Race Deck")),
                Task.Run(() => Create(" RACE DECK ")));

            Assert.Equal(1, results.Count(result => result));
            Assert.Equal(1, results.Count(result => !result));
            Assert.Equal(2, interceptor.InsertCount);
            using var verificationContext = new QuizManagementDbContext(setupOptions);
            Assert.Single(verificationContext.Decks);
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

    private static User CreateUser(string id, string email) => new()
    {
        Id = id,
        Username = id,
        Email = email,
        Role = AppRoles.User,
        SecurityStamp = Guid.NewGuid().ToString()
    };

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
