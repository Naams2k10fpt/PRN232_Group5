using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class UtcDateTimeSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void DateTimeConverter_MaterializesRequiredAndNullableValuesAsUtc()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseSqlServer(SqlServerTestConnection.ForDatabase(databaseName))
            .Options;
        var clockValue = new DateTime(2026, 7, 16, 9, 30, 0, DateTimeKind.Unspecified);

        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            setup.Users.Add(new User
            {
                Id = "utc-user",
                Username = "utc-user",
                Email = "utc-user@test.local",
                Role = AppRoles.User,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = clockValue,
                UpdatedAt = clockValue.AddMinutes(1)
            });
            setup.SaveChanges();
        }

        try
        {
            using var verification = new QuizManagementDbContext(options);
            var user = verification.Users.AsNoTracking().Single(candidate => candidate.Id == "utc-user");

            Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, user.UpdatedAt!.Value.Kind);
            Assert.Equal(clockValue, user.CreatedAt);
            Assert.Equal(clockValue.AddMinutes(1), user.UpdatedAt);
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }
}
