using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataAccessObjects;

public static class DbExceptionClassifier
{
    public static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
