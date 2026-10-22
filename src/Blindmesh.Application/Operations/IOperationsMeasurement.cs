namespace Blindmesh.Application.Operations;

/// <summary>
/// The state of the schema of a database of the product. The applied version is
/// the last migration the database holds and it is compared with the migration
/// the product expects.
/// </summary>
public sealed record RehearsalSchema(string AppliedVersion, bool MatchesExpected, int AppliedMigrations);

/// <summary>
/// The inventory of the objects of the private bucket. The count is the number
/// of the objects and the checksum is the checksum of the sorted keys of them.
/// </summary>
public sealed record RehearsalObjectInventory(int Count, string ChecksumSha256);

/// <summary>
/// The reconciliation of the credit ledger. Every transaction of the journal
/// sums to zero and no account with an owner holds a negative balance.
/// </summary>
public sealed record RehearsalLedger(
    long Transactions,
    long UnbalancedTransactions,
    long Accounts,
    long NegativeOwnerAccounts);

/// <summary>
/// The reading of the measured state of one database of the product. The live
/// database and the restored database of a rehearsal are read with the same
/// contract, so both sides of a comparison are measured the same way.
/// </summary>
public interface IOperationsDatabase
{
    /// <summary>The state of the schema of the database.</summary>
    Task<RehearsalSchema> ReadSchemaAsync(CancellationToken cancellationToken);

    /// <summary>The names of the tables of the database.</summary>
    Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken);

    /// <summary>The number of the rows of one table of the database.</summary>
    Task<long> CountRowsAsync(string table, CancellationToken cancellationToken);

    /// <summary>The inventory of the objects the database refers to.</summary>
    Task<RehearsalObjectInventory> ReadObjectInventoryAsync(CancellationToken cancellationToken);

    /// <summary>The reconciliation of the credit ledger of the database.</summary>
    Task<RehearsalLedger> ReconcileLedgerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The measurements of the live database of the product. Every value the pilot
/// gate reports is read here, so the gate never holds a target as a
/// measurement.
/// </summary>
public interface IOperationsMeasurement : IOperationsDatabase
{
    /// <summary>
    /// Runs one measured command of the gate and returns the value it
    /// produced. The command is a constant of the product, never a value of a
    /// request.
    /// </summary>
    Task<long> MeasureAsync(string command, CancellationToken cancellationToken);
}

/// <summary>
/// The isolated environment of one restore rehearsal. The implementation dumps
/// the live database, creates the isolated database and restores the dump into
/// it. The environment never runs inside the transaction of a command.
/// </summary>
public interface IRestoreRehearsalEnvironment
{
    /// <summary>
    /// Dumps the live database, creates the isolated database and restores the
    /// dump into it. The name of the isolated database is returned.
    /// </summary>
    Task<string> PrepareAsync(CancellationToken cancellationToken);

    /// <summary>A reader for the isolated database.</summary>
    IRehearsalDatabase OpenDatabase(string databaseName);

    /// <summary>Drops the isolated database.</summary>
    Task DropAsync(string databaseName, CancellationToken cancellationToken);
}

/// <summary>
/// A database of the product that a rehearsal reads. The restored database and
/// the live database are read with the same reader, so both sides of a
/// comparison are measured the same way.
/// </summary>
public interface IRehearsalDatabase : IOperationsDatabase, IDisposable
{
    /// <summary>The name of the database.</summary>
    string DatabaseName { get; }
}
