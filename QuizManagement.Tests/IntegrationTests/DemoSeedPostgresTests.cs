using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class DemoSeedPostgresTests
{
    [Fact]
    public void Migrations_MatchTheCurrentModel()
    {
        using var context = CreateContext("design_time_only");

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The EF model has changes that are not captured in a migration. Run 'dotnet ef migrations add'.");
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void MigrationsThenSeed_CreateLatestSchemaAndDemoData()
    {
        var databaseName = $"quizmanagementtests_{Guid.NewGuid():N}";
        using var context = CreateContext(databaseName);

        try
        {
            context.Database.Migrate();

            using var database = Open(databaseName);
            ExecuteSeed(database);

            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'EmailConfirmed'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'LoginAttempts' AND column_name = 'CountsTowardLockout'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'FlashcardProgresses'"));
            Assert.Equal(3, Scalar(database, "SELECT COUNT(*) FROM \"Users\" WHERE \"Id\" LIKE 'seed-%'"));
            Assert.Equal(21, Scalar(database, "SELECT COUNT(*) FROM \"Questions\""));
            Assert.Equal(16, Scalar(database, "SELECT COUNT(*) FROM \"StudyGroups\" WHERE \"CreatedByUserId\" LIKE 'seed-%'"));
            Assert.Equal(14, Scalar(database, "SELECT COUNT(*) FROM \"StudyGroups\" WHERE \"CreatedByUserId\" LIKE 'seed-%' AND \"AccessMode\" IN (0, 1)"));
            Assert.Equal(19, Scalar(database, "SELECT COUNT(*) FROM \"GroupMessages\" WHERE \"SenderUserId\" LIKE 'seed-%'"));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void Seed_RejectsDatabaseWithoutSchemaWithoutMutation()
    {
        var databaseName = $"quizmanagementtests_{Guid.NewGuid():N}";
        using var context = CreateContext(databaseName);

        try
        {
            using (var admin = Open("postgres"))
                new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin).ExecuteNonQuery();

            using var database = Open(databaseName);
            var exception = Assert.Throws<PostgresException>(() => ExecuteSeed(database));

            Assert.Contains("schema not found", exception.MessageText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, Scalar(database, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'"));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void UserSampleSeed_ClonesQuizContentWithoutReplacingExistingUserData()
    {
        var databaseName = $"quizmanagementtests_{Guid.NewGuid():N}";
        using var context = CreateContext(databaseName);
        try
        {
            context.Database.Migrate();
            using var database = Open(databaseName);
            ExecuteSeed(database);
            new NpgsqlCommand("""
                INSERT INTO "Users" ("Id", "Username", "Email", "Role", "IsDisabled", "SecurityStamp", "CreatedAt")
                VALUES ('outside-user', 'outside-user', 'outside@test.local', 'User', false, 'stamp', now());
                INSERT INTO "Subjects" ("UserId", "Name", "IsDeleted", "CreatedAt")
                VALUES ('outside-user', 'Existing subject', false, now());
                SET quiz.seed_target_user_id = 'outside-user';
                """, database).ExecuteNonQuery();

            var script = File.ReadAllText(RootFile("db", "seed-user-sample.sql"));
            new NpgsqlCommand(script, database).ExecuteNonQuery();
            new NpgsqlCommand(script, database).ExecuteNonQuery();

            Assert.Equal(3, Scalar(database, "SELECT COUNT(*) FROM \"Subjects\" WHERE \"UserId\" = 'outside-user'"));
            Assert.Equal(3, Scalar(database, "SELECT COUNT(*) FROM \"Decks\" d JOIN \"Subjects\" s ON s.\"Id\" = d.\"SubjectId\" WHERE s.\"UserId\" = 'outside-user'"));
            Assert.Equal(21, Scalar(database, "SELECT COUNT(*) FROM \"Questions\" q JOIN \"Decks\" d ON d.\"Id\" = q.\"DeckId\" JOIN \"Subjects\" s ON s.\"Id\" = d.\"SubjectId\" WHERE s.\"UserId\" = 'outside-user'"));
            Assert.Equal(84, Scalar(database, "SELECT COUNT(*) FROM \"Answers\" a JOIN \"Questions\" q ON q.\"Id\" = a.\"QuestionId\" JOIN \"Decks\" d ON d.\"Id\" = q.\"DeckId\" JOIN \"Subjects\" s ON s.\"Id\" = d.\"SubjectId\" WHERE s.\"UserId\" = 'outside-user'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"Subjects\" WHERE \"UserId\" = 'outside-user' AND \"Name\" = 'Existing subject'"));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public void Seed_RerunsAfterDemoActivityWithoutDeletingUnrelatedData()
    {
        var databaseName = $"quizmanagementtests_{Guid.NewGuid():N}";
        using var context = CreateContext(databaseName);

        try
        {
            context.Database.Migrate();

            using var database = Open(databaseName);
            ExecuteSeed(database);
            AddDemoActivityAndUnrelatedData(database);

            ExecuteSeed(database);

            Assert.Equal(3, Scalar(database, "SELECT COUNT(*) FROM \"Users\" WHERE \"Id\" LIKE 'seed-%'"));
            Assert.Equal(16, Scalar(database, "SELECT COUNT(*) FROM \"StudyGroups\" WHERE \"CreatedByUserId\" LIKE 'seed-%'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"Users\" WHERE \"Id\" = 'outside-user'"));
            Assert.Equal(2, Scalar(database, "SELECT COUNT(*) FROM \"QuestionReports\" WHERE \"UserId\" = 'seed-user-001'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"QuestionReports\" WHERE \"UserId\" = 'seed-user-001' AND NOT \"IsResolved\""));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"QuestionReports\" WHERE \"UserId\" = 'seed-user-001' AND \"IsResolved\""));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"QuizAttempts\" WHERE \"UserId\" = 'seed-user-001' AND \"CompletedAtUtc\" IS NOT NULL"));
            Assert.Equal(0, Scalar(database, "SELECT COUNT(*) FROM \"QuizAttempts\" WHERE \"UserId\" = 'seed-user-001' AND \"CompletedAtUtc\" IS NULL"));
            Assert.Equal(1, Scalar(database, """
                SELECT COUNT(*)
                FROM "TestHistories"
                WHERE "UserId" = 'seed-user-001'
                  AND "QuizAttemptId" IS NOT NULL
                  AND "ResultSnapshotJson" IS JSON
                  AND "ResultSnapshotJson"::json ->> 'DeckName' = 'ASP.NET Core MVC'
                """));
            Assert.Equal(5, Scalar(database, """
                SELECT COUNT(*)
                FROM "TestHistories" history
                CROSS JOIN LATERAL json_array_elements(history."ResultSnapshotJson"::json -> 'Questions')
                WHERE history."UserId" = 'seed-user-001'
                """));
            Assert.Equal(7, Scalar(database, """
                SELECT COUNT(*)
                FROM "TestResultDetails" detail
                JOIN "TestHistories" history ON history."Id" = detail."TestHistoryId"
                WHERE history."UserId" = 'seed-user-001'
                """));
            Assert.Equal(2, Scalar(database, "SELECT COUNT(*) FROM \"LoginAttempts\" WHERE \"Email\" = 'user.demo@quiz.local'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"LoginAttempts\" WHERE \"Email\" = 'unknown.demo@quiz.local'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM \"LoginAttempts\" WHERE \"Email\" = 'outside@test.local'"));
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    private static void AddDemoActivityAndUnrelatedData(NpgsqlConnection database)
    {
        new NpgsqlCommand("""
            INSERT INTO "Users"
                ("Id", "Username", "Email", "PasswordHash", "Role", "IsDisabled", "SecurityStamp", "CreatedAt")
            VALUES
                ('outside-user', 'outside-user', 'outside@test.local', NULL, 'User', false, 'stamp', now());

            INSERT INTO "QuestionReports" ("QuestionId", "UserId", "Reason", "IsResolved", "CreatedAt")
            SELECT question_id, reporter, reason, false, now()
            FROM (
                SELECT q."Id" AS question_id
                FROM "Questions" q
                JOIN "Decks" d ON d."Id" = q."DeckId"
                JOIN "Subjects" s ON s."Id" = d."SubjectId"
                WHERE s."UserId" = 'seed-mentor-001'
                ORDER BY q."Id"
                LIMIT 1
            ) question
            CROSS JOIN (VALUES ('seed-user-001', 'WrongAnswer'), ('outside-user', 'Other')) AS reports (reporter, reason);

            INSERT INTO "LoginAttempts" ("Email", "IpAddress", "IsSuccess", "UserId", "CreatedAt")
            VALUES
                ('user.demo@quiz.local', '127.0.0.1', true, 'seed-user-001', now()),
                ('outside@test.local', '127.0.0.2', true, 'outside-user', now());

            INSERT INTO "QuizAttempts"
                ("Id", "UserId", "DeckId", "QuestionIdsJson", "TimeLimitMinutes", "StartedAtUtc")
            SELECT gen_random_uuid(), 'seed-user-001', d."Id", '[]', 0, now()
            FROM "Decks" d
            JOIN "Subjects" s ON s."Id" = d."SubjectId"
            WHERE s."UserId" = 'seed-mentor-001'
            ORDER BY d."Id"
            LIMIT 1;
            """, database).ExecuteNonQuery();
    }

    private static QuizManagementDbContext CreateContext(string databaseName)
        => new(new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase(databaseName))
            .Options);

    private static NpgsqlConnection Open(string databaseName)
    {
        var connection = new NpgsqlConnection(PostgresTestConnection.ForDatabase(databaseName));
        connection.Open();
        return connection;
    }

    private static void ExecuteSeed(NpgsqlConnection database)
        => new NpgsqlCommand(File.ReadAllText(RootFile("db", "seed-demo.sql")), database) { CommandTimeout = 60 }
            .ExecuteNonQuery();

    private static string RootFile(params string[] segments)
        => Path.GetFullPath(Path.Combine(
            [AppContext.BaseDirectory, "..", "..", "..", "..", .. segments]));

    private static int Scalar(NpgsqlConnection connection, string sql)
        => Convert.ToInt32(new NpgsqlCommand(sql, connection).ExecuteScalar());
}
