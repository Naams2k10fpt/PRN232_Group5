using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public class DemoSeedSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void CreateScript_CreatesLatestSchemaAndDemoDataInOneRun()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        using var master = new SqlConnection(SqlServerTestConnection.ForDatabase("master"));
        master.Open();

        try
        {
            ExecuteBootstrap(master, databaseName);

            using var database = new SqlConnection(SqlServerTestConnection.ForDatabase(databaseName));
            database.Open();
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'EmailConfirmed'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LoginAttempts') AND name = N'CountsTowardLockout'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM sys.tables WHERE name = N'FlashcardProgresses'"));
            Assert.Equal(3, Scalar(database, "SELECT COUNT(*) FROM dbo.Users WHERE Id LIKE N'seed-%'"));
            Assert.Equal(21, Scalar(database, "SELECT COUNT(*) FROM dbo.Questions"));
        }
        finally
        {
            new SqlCommand(
                $"USE [master]; ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]",
                master).ExecuteNonQuery();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void CreateScript_RejectsPartialSchemaWithoutMutation()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var masterConnectionString = SqlServerTestConnection.ForDatabase("master");
        var databaseConnectionString = SqlServerTestConnection.ForDatabase(databaseName);
        using var master = new SqlConnection(masterConnectionString);
        master.Open();
        new SqlCommand($"CREATE DATABASE [{databaseName}]", master).ExecuteNonQuery();

        try
        {
            using var database = new SqlConnection(databaseConnectionString);
            database.Open();
            new SqlCommand("CREATE TABLE dbo.Users (Id INT NOT NULL PRIMARY KEY)", database).ExecuteNonQuery();

            var exception = Assert.Throws<SqlException>(() => ExecuteBootstrap(database, databaseName));

            Assert.Equal(51020, exception.Number);
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0"));
        }
        finally
        {
            new SqlCommand(
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]",
                master).ExecuteNonQuery();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public void CreateScript_RerunsAfterDemoActivityWithoutDeletingUnrelatedData()
    {
        var databaseName = $"QuizManagementTests_{Guid.NewGuid():N}";
        var masterConnectionString = SqlServerTestConnection.ForDatabase("master");
        var databaseConnectionString = SqlServerTestConnection.ForDatabase(databaseName);
        using var master = new SqlConnection(masterConnectionString);
        master.Open();

        try
        {
            ExecuteBootstrap(master, databaseName);

            using var database = new SqlConnection(databaseConnectionString);
            database.Open();
            AddDemoActivityAndUnrelatedData(database);

            ExecuteBootstrap(database, databaseName);

            Assert.Equal(3, Scalar(database, "SELECT COUNT(*) FROM dbo.Users WHERE Id LIKE N'seed-%'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM dbo.Users WHERE Id = N'outside-user'"));
            Assert.Equal(2, Scalar(database, "SELECT COUNT(*) FROM dbo.QuestionReports WHERE UserId = N'seed-user-001'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM dbo.QuestionReports WHERE UserId = N'seed-user-001' AND IsResolved = 0"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM dbo.QuestionReports WHERE UserId = N'seed-user-001' AND IsResolved = 1"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM dbo.QuizAttempts WHERE UserId = N'seed-user-001' AND CompletedAtUtc IS NOT NULL"));
            Assert.Equal(0, Scalar(database, "SELECT COUNT(*) FROM dbo.QuizAttempts WHERE UserId = N'seed-user-001' AND CompletedAtUtc IS NULL"));
            Assert.Equal(1, Scalar(database, """
                SELECT COUNT(*)
                FROM dbo.TestHistories
                WHERE UserId = N'seed-user-001'
                  AND QuizAttemptId IS NOT NULL
                  AND ISJSON(ResultSnapshotJson) = 1
                  AND JSON_VALUE(ResultSnapshotJson, '$.DeckName') = N'ASP.NET Core MVC'
                """));
            Assert.Equal(5, Scalar(database, """
                SELECT COUNT(*)
                FROM dbo.TestHistories history
                CROSS APPLY OPENJSON(history.ResultSnapshotJson, '$.Questions') questions
                WHERE history.UserId = N'seed-user-001'
                """));
            Assert.Equal(7, Scalar(database, """
                SELECT COUNT(*)
                FROM dbo.TestResultDetails detail
                JOIN dbo.TestHistories history ON history.Id = detail.TestHistoryId
                WHERE history.UserId = N'seed-user-001'
                """));
            Assert.Equal(2, Scalar(database, "SELECT COUNT(*) FROM dbo.LoginAttempts WHERE Email = N'user.demo@quiz.local'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM dbo.LoginAttempts WHERE Email = N'unknown.demo@quiz.local'"));
            Assert.Equal(1, Scalar(database, "SELECT COUNT(*) FROM dbo.LoginAttempts WHERE Email = N'outside@test.local'"));
        }
        finally
        {
            new SqlCommand(
                $"USE [master]; ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]",
                master).ExecuteNonQuery();
        }
    }

    private static void AddDemoActivityAndUnrelatedData(SqlConnection database)
    {
        new SqlCommand("""
            INSERT INTO dbo.Users
                (Id, Username, Email, PasswordHash, Role, IsDisabled, SecurityStamp, CreatedAt)
            VALUES
                (N'outside-user', N'outside-user', N'outside@test.local', NULL, N'User', 0, N'stamp', SYSUTCDATETIME());

            DECLARE @QuestionId INT = (
                SELECT TOP (1) q.Id
                FROM dbo.Questions q
                JOIN dbo.Decks d ON d.Id = q.DeckId
                JOIN dbo.Subjects s ON s.Id = d.SubjectId
                WHERE s.UserId = N'seed-mentor-001');
            DECLARE @DeckId INT = (
                SELECT TOP (1) d.Id
                FROM dbo.Decks d
                JOIN dbo.Subjects s ON s.Id = d.SubjectId
                WHERE s.UserId = N'seed-mentor-001');

            INSERT INTO dbo.QuestionReports (QuestionId, UserId, Reason, IsResolved, CreatedAt)
            VALUES
                (@QuestionId, N'seed-user-001', N'WrongAnswer', 0, SYSUTCDATETIME()),
                (@QuestionId, N'outside-user', N'Other', 0, SYSUTCDATETIME());

            INSERT INTO dbo.LoginAttempts (Email, IpAddress, IsSuccess, UserId, CreatedAt)
            VALUES
                (N'user.demo@quiz.local', N'127.0.0.1', 1, N'seed-user-001', SYSUTCDATETIME()),
                (N'outside@test.local', N'127.0.0.2', 1, N'outside-user', SYSUTCDATETIME());

            INSERT INTO dbo.QuizAttempts
                (Id, UserId, DeckId, QuestionIdsJson, TimeLimitMinutes, StartedAtUtc)
            VALUES
                (NEWID(), N'seed-user-001', @DeckId, N'[]', 0, SYSDATETIMEOFFSET());
            """, database).ExecuteNonQuery();
    }

    private static void ExecuteBootstrap(SqlConnection database, string databaseName)
    {
        var path = RootFile("CreateDB.sql");
        var script = File.ReadAllText(path)
            .Replace("QuizManagementDB", databaseName, StringComparison.Ordinal);
        ExecuteBatches(database, script);
    }

    private static void ExecuteBatches(SqlConnection database, string script)
    {
        foreach (var batch in Regex.Split(
                     script,
                     @"^\s*GO\s*(?:--.*)?$",
                     RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(batch))
                new SqlCommand(batch, database) { CommandTimeout = 60 }.ExecuteNonQuery();
        }
    }

    private static string RootFile(string fileName)
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            fileName));

    private static int Scalar(SqlConnection connection, string sql)
        => Convert.ToInt32(new SqlCommand(sql, connection).ExecuteScalar());
}
