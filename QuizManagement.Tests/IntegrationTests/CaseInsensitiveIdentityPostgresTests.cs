using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

// PostgreSQL compares text case-sensitively; SQL Server's default collation did not.
// These tests pin the case-insensitive behaviour the app relied on.
public class CaseInsensitiveIdentityPostgresTests
{
    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void UserLookupsAndUniqueness_IgnoreCase()
    {
        var options = CreateOptions();
        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            setup.Users.Add(CreateUser("user-1", "Minh.Anh", "Minh.Anh@Test.Local"));
            setup.SaveChanges();
        }

        try
        {
            using (var context = new QuizManagementDbContext(options))
            {
                var repository = new UserRepository(context);
                Assert.Equal("user-1", repository.GetByEmail("minh.anh@test.local")?.Id);
                Assert.Equal("user-1", repository.GetByEmail("MINH.ANH@TEST.LOCAL")?.Id);
                Assert.Equal("user-1", repository.GetByUsername("minh.anh")?.Id);
            }

            using (var context = new QuizManagementDbContext(options))
            {
                var service = new UserService(new UserRepository(context));
                Assert.False(service.TryCreateUser(CreateUser("user-2", "other", "MINH.ANH@test.local")));
            }
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void SubjectAndDeckNames_IgnoreCaseIncludingVietnameseLetters()
    {
        var options = CreateOptions();
        int subjectId;
        using (var setup = new QuizManagementDbContext(options))
        {
            setup.Database.EnsureCreated();
            setup.Users.Add(CreateUser("mentor", "mentor", "mentor@test.local"));
            var subject = new Subject { UserId = "mentor", Name = "Đề cương ôn tập" };
            subject.Decks.Add(new Deck { Name = "Bộ đề số một" });
            setup.Subjects.Add(subject);
            setup.SaveChanges();
            subjectId = subject.Id;
        }

        try
        {
            using (var context = new QuizManagementDbContext(options))
            {
                Assert.True(SubjectDAO.Instance.NameExists(context, "mentor", "ĐỀ CƯƠNG ÔN TẬP"));
                Assert.True(DeckDAO.Instance.NameExists(context, subjectId, "BỘ ĐỀ SỐ MỘT"));
                Assert.False(SubjectDAO.Instance.NameExists(context, "mentor", "Đề cương khác"));
            }

            using (var context = new QuizManagementDbContext(options))
            {
                context.Subjects.Add(new Subject { UserId = "mentor", Name = "ĐỀ CƯƠNG ÔN TẬP" });
                var exception = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
                Assert.True(DbExceptionClassifier.IsUniqueViolation(exception));
            }
        }
        finally
        {
            using var cleanup = new QuizManagementDbContext(options);
            cleanup.Database.EnsureDeleted();
        }
    }

    private static DbContextOptions<QuizManagementDbContext> CreateOptions()
        => new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}"))
            .Options;

    private static User CreateUser(string id, string username, string email) => new()
    {
        Id = id,
        Username = username,
        Email = email,
        Role = AppRoles.Mentor,
        SecurityStamp = Guid.NewGuid().ToString()
    };
}
