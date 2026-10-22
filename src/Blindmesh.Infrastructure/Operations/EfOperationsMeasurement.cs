using Blindmesh.Application.Operations;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Operations;

/// <summary>
/// The measurements of the live database of the product. Every value of the
/// pilot gate is read here, so the gate never holds a target as a
/// measurement.
/// </summary>
public sealed class EfOperationsMeasurement(BlindmeshDbContext dbContext) : IOperationsMeasurement
{
    private readonly OperationsDatabaseReader reader = new(dbContext);

    public Task<RehearsalSchema> ReadSchemaAsync(CancellationToken cancellationToken) =>
        reader.ReadSchemaAsync(cancellationToken);

    public Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken) =>
        reader.ListTablesAsync(cancellationToken);

    public Task<long> CountRowsAsync(string table, CancellationToken cancellationToken) =>
        reader.CountRowsAsync(table, cancellationToken);

    public Task<RehearsalObjectInventory> ReadObjectInventoryAsync(CancellationToken cancellationToken) =>
        reader.ReadObjectInventoryAsync(cancellationToken);

    public Task<RehearsalLedger> ReconcileLedgerAsync(CancellationToken cancellationToken) =>
        reader.ReconcileLedgerAsync(cancellationToken);

    public Task<long> MeasureAsync(string command, CancellationToken cancellationToken) =>
        reader.MeasureAsync(command, cancellationToken);
}

/// <summary>
/// A reader for the isolated database of one restore rehearsal. The database
/// was restored from a dump of the live database, and it is read with the same
/// reader as the live database.
/// </summary>
public sealed class RehearsalDatabase : IRehearsalDatabase
{
    private readonly BlindmeshDbContext dbContext;
    private readonly OperationsDatabaseReader reader;

    public RehearsalDatabase(string databaseName, string connectionString)
    {
        DatabaseName = databaseName;

        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(BlindmeshDbContext).Assembly.GetName().Name))
            .Options;

        dbContext = new BlindmeshDbContext(options);
        reader = new OperationsDatabaseReader(dbContext);
    }

    public string DatabaseName { get; }

    public Task<RehearsalSchema> ReadSchemaAsync(CancellationToken cancellationToken) =>
        reader.ReadSchemaAsync(cancellationToken);

    public Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken) =>
        reader.ListTablesAsync(cancellationToken);

    public Task<long> CountRowsAsync(string table, CancellationToken cancellationToken) =>
        reader.CountRowsAsync(table, cancellationToken);

    public Task<RehearsalObjectInventory> ReadObjectInventoryAsync(CancellationToken cancellationToken) =>
        reader.ReadObjectInventoryAsync(cancellationToken);

    public Task<RehearsalLedger> ReconcileLedgerAsync(CancellationToken cancellationToken) =>
        reader.ReconcileLedgerAsync(cancellationToken);

    /// <summary>
    /// Closes the connection to the isolated database, so the database may be
    /// dropped after the rehearsal.
    /// </summary>
    public void Dispose()
    {
        dbContext.Database.CloseConnection();
        dbContext.Dispose();
    }
}
