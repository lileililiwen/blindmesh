using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Blindmesh.Application.Operations;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Findings;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Operations;

/// <summary>
/// The reader of the measured state of one database of the product. The live
/// database and the restored database of a rehearsal are read with the same
/// reader, so both sides of a comparison are measured the same way.
/// </summary>
public sealed class OperationsDatabaseReader(BlindmeshDbContext dbContext)
{
    /// <summary>The table of the migration history of the product.</summary>
    private const string MigrationTable = "__EFMigrationsHistory";

    /// <summary>The state of the schema of the database.</summary>
    public async Task<RehearsalSchema> ReadSchemaAsync(CancellationToken cancellationToken)
    {
        var applied = await ReadAppliedMigrationAsync(cancellationToken);
        var expected = dbContext.Database.GetMigrations().ToArray();

        var expectedVersion = expected.Length > 0 ? expected[^1] : string.Empty;

        return new RehearsalSchema(
            applied,
            !string.IsNullOrEmpty(expectedVersion)
                && string.Equals(applied, expectedVersion, StringComparison.Ordinal),
            expected.Length);
    }

    /// <summary>The names of the tables of the database.</summary>
    public async Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken)
    {
        var tables = new List<string>();

        await using var command = await CreateCommandAsync(
            "SELECT table_name AS \"Value\" FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>The number of the rows of one table of the database.</summary>
    public async Task<long> CountRowsAsync(string table, CancellationToken cancellationToken)
    {
        var quoted = QuoteIdentifier(table);

        await using var command = await CreateCommandAsync($"SELECT count(*)::bigint FROM {quoted}");

        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The inventory of the objects the database refers to. The count is the
    /// number of the objects and the checksum is the checksum of the sorted
    /// keys of them.
    /// </summary>
    public async Task<RehearsalObjectInventory> ReadObjectInventoryAsync(CancellationToken cancellationToken)
    {
        var keys = await dbContext.Attachments
            .AsNoTracking()
            .OrderBy(attachment => attachment.StorageKey)
            .Select(attachment => attachment.StorageKey)
            .ToListAsync(cancellationToken);

        if (keys.Count == 0)
        {
            return new RehearsalObjectInventory(0, ChecksumOf(Array.Empty<string>()));
        }

        return new RehearsalObjectInventory(keys.Count, ChecksumOf(keys));
    }

    /// <summary>The reconciliation of the credit ledger of the database.</summary>
    public async Task<RehearsalLedger> ReconcileLedgerAsync(CancellationToken cancellationToken)
    {
        var transactions = await dbContext.LedgerTransactions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var sums = await dbContext.LedgerEntries
            .AsNoTracking()
            .GroupBy(entry => entry.TransactionId)
            .Select(group => group.Sum(entry => entry.Amount))
            .ToListAsync(cancellationToken);

        var unbalanced = sums.Count(sum => sum != 0);

        var balances = await dbContext.LedgerEntries
            .AsNoTracking()
            .GroupBy(entry => entry.AccountId)
            .Select(group => new { AccountId = group.Key, Balance = group.Sum(entry => entry.Amount) })
            .ToListAsync(cancellationToken);

        var ownerAccounts = await dbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.OwnerId != null)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);

        var owners = new HashSet<Guid>(ownerAccounts);
        var negative = balances.Count(balance => owners.Contains(balance.AccountId) && balance.Balance < 0);

        return new RehearsalLedger(transactions, unbalanced, balances.Count, negative);
    }

    /// <summary>
    /// Runs one measured command and returns the value it produced. The command
    /// is a constant of the product and it holds no value of a request.
    /// </summary>
    public async Task<long> MeasureAsync(string command, CancellationToken cancellationToken)
    {
        await using var sql = await CreateCommandAsync(command);

        var value = await sql.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private async Task<string> ReadAppliedMigrationAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();

        if (!await TableExistsAsync(MigrationTable, cancellationToken))
        {
            return string.Empty;
        }

        await using var command = await CreateCommandAsync(
            $"SELECT \"MigrationId\" FROM {QuoteIdentifier(MigrationTable)} ORDER BY \"MigrationId\" DESC LIMIT 1");

        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value as string ?? string.Empty;
    }

    private async Task<bool> TableExistsAsync(string table, CancellationToken cancellationToken)
    {
        await using var command = await CreateCommandAsync(
            "SELECT count(*)::bigint FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = @table");

        var parameter = command.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is not null and not DBNull && Convert.ToInt64(value, CultureInfo.InvariantCulture) > 0;
    }

    private async Task<DbCommand> CreateCommandAsync(string sql)
    {
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;

        var transaction = dbContext.Database.CurrentTransaction;

        if (transaction is not null)
        {
            command.Transaction = transaction.GetDbTransaction();
        }

        return command;
    }

    /// <summary>
    /// Quotes the name of a table. The name comes from the catalog of the
    /// database, so it never holds a value of a request.
    /// </summary>
    private static string QuoteIdentifier(string name) =>
        $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>
    /// The checksum of the sorted keys of the objects. The keys are joined with
    /// a line feed, so the checksum of the same set of keys is always the same.
    /// </summary>
    private static string ChecksumOf(IReadOnlyList<string> keys)
    {
        var joined = string.Join("\n", keys);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }
}
