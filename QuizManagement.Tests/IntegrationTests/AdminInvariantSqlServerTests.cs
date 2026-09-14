using System.Data.Common;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class AdminInvariantSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void AdminMutation_RejectsLastActiveAdminAndAllowsMutationWhenAnotherRemains()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;

        using var context = new QuizManagementDbContext(options);
        try
        {
            context.Database.EnsureCreated();
            var first = CreateAdmin("admin-a");
            context.Users.Add(first);
            context.SaveChanges();
            var service = new AdminService(new AdminRepository(context));

            Assert.Equal(
                AdminMutationResult.LastActiveAdmin,
                service.ChangeRole(first.Id, AppRoles.Mentor));
            Assert.Equal(
                AdminMutationResult.LastActiveAdmin,
                service.SetDisabled(first.Id, true));
            Assert.Equal(AppRoles.Admin, first.Role);
            Assert.False(first.IsDisabled);

            var second = CreateAdmin("admin-b");
            context.Users.Add(second);
            context.SaveChanges();
            var oldStamp = first.SecurityStamp;

            Assert.Equal(
                AdminMutationResult.Updated,
                service.ChangeRole(first.Id, AppRoles.Mentor));
            Assert.Equal(AdminMutationResult.NotFound, service.SetDisabled("missing", true));

            context.ChangeTracker.Clear();
            var persisted = context.Users.Single(user => user.Id == first.Id);
            Assert.Equal(AppRoles.Mentor, persisted.Role);
            Assert.NotEqual(oldStamp, persisted.SecurityStamp);
            Assert.Equal(1, context.Users.Count(user => user.Role == AppRoles.Admin && !user.IsDisabled));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentDestructiveMutations_PreserveOneActiveAdmin(bool disableSecond)
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var setupOptions = CreateOptions(databaseName);
        using (var setupContext = new QuizManagementDbContext(setupOptions))
        {
            setupContext.Database.EnsureCreated();
            setupContext.Users.AddRange(CreateAdmin("admin-a"), CreateAdmin("admin-b"));
            setupContext.SaveChanges();
        }

        try
        {
            using var barrier = new Barrier(2);
            var interceptor = new AdminLockBarrierInterceptor(barrier);
            var workerOptions = CreateOptions(databaseName, interceptor);

            AdminMutationResult Demote(string userId)
            {
                using var context = new QuizManagementDbContext(workerOptions);
                return new AdminService(new AdminRepository(context))
                    .ChangeRole(userId, AppRoles.Mentor);
            }

            AdminMutationResult Disable(string userId)
            {
                using var context = new QuizManagementDbContext(workerOptions);
                return new AdminService(new AdminRepository(context))
                    .SetDisabled(userId, true);
            }

            var results = await Task.WhenAll(
                Task.Run(() => Demote("admin-a")),
                Task.Run(() => disableSecond ? Disable("admin-b") : Demote("admin-b")));

            Assert.Equal(1, results.Count(result => result == AdminMutationResult.Updated));
            Assert.Equal(1, results.Count(result => result == AdminMutationResult.LastActiveAdmin));
            Assert.Equal(2, interceptor.LockQueryCount);
            using var verificationContext = new QuizManagementDbContext(setupOptions);
            Assert.Equal(1, verificationContext.Users.Count(
                user => user.Role == AppRoles.Admin && !user.IsDisabled));
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

    private static User CreateAdmin(string id) => new()
    {
        Id = id,
        Username = id,
        Email = $"{id}@test.local",
        Role = AppRoles.Admin,
        SecurityStamp = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow
    };

    private sealed class AdminLockBarrierInterceptor(Barrier barrier) : DbCommandInterceptor
    {
        private int _lockQueryCount;

        public int LockQueryCount => Volatile.Read(ref _lockQueryCount);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _lockQueryCount);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Concurrent Admin lock queries did not reach the SQL barrier.");
            }

            return result;
        }
    }
}
