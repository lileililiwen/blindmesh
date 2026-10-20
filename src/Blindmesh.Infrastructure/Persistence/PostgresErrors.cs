using Npgsql;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Recognises the PostgreSQL error conditions the product reacts to. The API
/// layer uses it instead of reading provider messages.
/// </summary>
public static class PostgresErrors
{
    private const string UniqueViolation = "23505";
    private const string SerializationFailure = "40001";
    private const string DeadlockDetected = "40P01";

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

    /// <summary>
    /// True when the failure is a serialization failure or a deadlock of the
    /// database. The command may be retried with the same key and payload.
    /// </summary>
    public static bool IsRetryableConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                (postgresException.SqlState == SerializationFailure || postgresException.SqlState == DeadlockDetected))
            {
                return true;
            }
        }

        return false;
    }
}
