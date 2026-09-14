using Microsoft.Data.SqlClient;

namespace QuizManagement.Tests.IntegrationTests;

internal static class SqlServerTestConnection
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ForDatabase(string databaseName)
    {
        var connectionString = Environment.GetEnvironmentVariable("QUIZ_TEST_SQLSERVER_CONNECTION")
            ?? DefaultConnectionString;
        return new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName }.ConnectionString;
    }
}
