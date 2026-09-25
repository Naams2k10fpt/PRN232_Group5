using Npgsql;

namespace QuizManagement.Tests.IntegrationTests;

internal static class PostgresTestConnection
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    public static string ForDatabase(string databaseName)
    {
        var connectionString = Environment.GetEnvironmentVariable("QUIZ_TEST_POSTGRES_CONNECTION")
            ?? DefaultConnectionString;
        return new NpgsqlConnectionStringBuilder(connectionString) { Database = databaseName }.ConnectionString;
    }
}
