using Npgsql;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Recognises the PostgreSQL error conditions the product reacts to. The API
/// layer uses it instead of reading provider messages.
/// </summary>
public static class PostgresErrors
{
    private const string UniqueViolation = "23505";

    /// <summary>
    /// True when the failure is a unique constraint violation on the named
    /// constraint or index.
    /// </summary>
    public static bool IsUniqueViolation(Exception exception, string constraintName)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState == UniqueViolation &&
                string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
