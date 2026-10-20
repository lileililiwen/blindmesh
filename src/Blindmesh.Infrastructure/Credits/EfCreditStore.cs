using Blindmesh.Application.Credits;
using Blindmesh.Domain.Credits;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Credits;

/// <summary>
/// The persistence of the credit ledger. This is the only place that writes a
/// ledger row. The journal is append only: the entries are inserted and never
/// updated, and a correction is a compensating transaction.
/// </summary>
public sealed class EfCreditStore(BlindmeshDbContext dbContext) : ICreditStore
{
    private const string PendingSource = "pending";

    public async Task<Guid> EnsureAccountAsync(
        LedgerBucket bucket,
        Guid? ownerId,
        string reference,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var id = CreditAccountIds.Derive(bucket, ownerId, reference);

        if (ownerId is null)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO ledger_accounts (id, bucket, owner_id, reference, created_at)
                VALUES ({0}, {1}, NULL, {2}, {3})
                ON CONFLICT DO NOTHING
                """,
                [id, bucket.ToString(), reference, now],
                cancellationToken);
        }
        else
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO ledger_accounts (id, bucket, owner_id, reference, created_at)
                VALUES ({0}, {1}, {2}, {3}, {4})
                ON CONFLICT DO NOTHING
                """,
                [id, bucket.ToString(), ownerId.Value, reference, now],
                cancellationToken);
        }

        return id;
    }

    public async Task<IReadOnlyList<Guid>> LockAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return [];
        }

        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A credit command must run inside the transaction of the caller.");

        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT id FROM ledger_accounts WHERE id = ANY(@ids) ORDER BY id FOR UPDATE";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "ids";
        parameter.Value = accountIds.ToArray();
        command.Parameters.Add(parameter);

        var locked = new List<Guid>(accountIds.Count);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            locked.Add(reader.GetGuid(0));
        }

        return locked;
    }

    public async Task<long> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var balances = await dbContext.LedgerEntries
            .Where(entry => entry.AccountId == accountId)
            .GroupBy(entry => entry.AccountId)
            .Select(group => group.Sum(entry => entry.Amount))
            .ToListAsync(cancellationToken);

        return balances.Count == 0 ? 0 : balances[0];
    }

    public async Task<IReadOnlyDictionary<Guid, long>> GetBalancesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var balances = await dbContext.LedgerEntries
            .Where(entry => accountIds.Contains(entry.AccountId))
            .GroupBy(entry => entry.AccountId)
            .Select(group => new { AccountId = group.Key, Amount = group.Sum(entry => entry.Amount) })
            .ToListAsync(cancellationToken);

        return balances.ToDictionary(balance => balance.AccountId, balance => balance.Amount);
    }

    public async Task<IReadOnlyList<LedgerAccount>> ListAccountsAsync(
        Guid ownerId,
        CancellationToken cancellationToken) =>
        await dbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

    public async Task AddTransactionAsync(
        LedgerTransaction transaction,
        IReadOnlyList<LedgerEntry> entries,
        CancellationToken cancellationToken)
    {
        dbContext.LedgerTransactions.Add(transaction);

        foreach (var entry in entries)
        {
            dbContext.LedgerEntries.Add(entry);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditLot>> ListLotsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.CreditLots
            .Where(lot => lot.UserId == userId)
            .OrderBy(lot => lot.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddLotAsync(CreditLot lot, CancellationToken cancellationToken)
    {
        dbContext.CreditLots.Add(lot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CreditLot?> FindPendingLotAsync(
        Guid userId,
        string reference,
        CancellationToken cancellationToken) =>
        dbContext.CreditLots
            .FirstOrDefaultAsync(lot => lot.UserId == userId && lot.Source == PendingSource && lot.Reference == reference, cancellationToken);

    public async Task SaveLotAsync(CreditLot lot, CancellationToken cancellationToken)
    {
        dbContext.CreditLots.Update(lot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<GrantClaim?> FindGrantClaimAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken) =>
        dbContext.GrantClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(claim => claim.Provider == provider && claim.Subject == subject, cancellationToken);

    public async Task AddGrantClaimAsync(GrantClaim claim, CancellationToken cancellationToken)
    {
        dbContext.GrantClaims.Add(claim);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> SumIssuedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var issued = await dbContext.GrantClaims
            .Where(claim => claim.IssuedAt >= since)
            .Select(claim => claim.Amount)
            .SumAsync(cancellationToken);

        return issued;
    }

    public async Task<int> CountClaimsSinceAsync(
        string address,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        await dbContext.GrantClaims
            .CountAsync(claim => claim.IpAddress == address && claim.IssuedAt >= since, cancellationToken);

    public async Task<IReadOnlyList<LedgerTransaction>> ListTransactionsAsync(
        IReadOnlyList<Guid> accountIds,
        int limit,
        CreditCursor? cursor,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return [];
        }

        var transactions = dbContext.LedgerTransactions
            .AsNoTracking()
            .Where(transaction => dbContext.LedgerEntries.Any(entry =>
                entry.TransactionId == transaction.Id && accountIds.Contains(entry.AccountId)));

        if (cursor is not null)
        {
            transactions = transactions.Where(transaction =>
                transaction.CreatedAt < cursor.CreatedAt ||
                (transaction.CreatedAt == cursor.CreatedAt && transaction.Id < cursor.Id));
        }

        return await transactions
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerEntry>> ListEntriesAsync(
        IReadOnlyList<Guid> transactionIds,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (transactionIds.Count == 0 || accountIds.Count == 0)
        {
            return [];
        }

        return await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => transactionIds.Contains(entry.TransactionId) && accountIds.Contains(entry.AccountId))
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
    }

    public bool IsRetryableConflict(Exception exception) => PostgresErrors.IsRetryableConflict(exception);
}
