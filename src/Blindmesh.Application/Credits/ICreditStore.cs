using Blindmesh.Domain.Credits;

namespace Blindmesh.Application.Credits;

/// <summary>
/// The persistence of the credit ledger. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly. The store is the only place that writes a ledger row.
/// </summary>
public interface ICreditStore
{
    /// <summary>
    /// The id of the account of the bucket. The account is created when it does
    /// not exist yet. The id is derived from the bucket, the owner and the
    /// reference, so two requests never create the same account twice.
    /// </summary>
    Task<Guid> EnsureAccountAsync(
        LedgerBucket bucket,
        Guid? ownerId,
        string reference,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Locks the accounts for the rest of the transaction. The lock is taken in
    /// the order of the identifiers, so two commands never wait for each other.
    /// The caller must hold an open transaction.
    /// </summary>
    Task<IReadOnlyList<Guid>> LockAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);

    /// <summary>The signed balance of one account, rebuilt from the journal.</summary>
    Task<long> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>The signed balances of the accounts, rebuilt from the journal.</summary>
    Task<IReadOnlyDictionary<Guid, long>> GetBalancesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);

    /// <summary>The ids of the accounts of a contributor.</summary>
    Task<IReadOnlyList<LedgerAccount>> ListAccountsAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>Writes a transaction with its entries. The entries are never changed.</summary>
    Task AddTransactionAsync(
        LedgerTransaction transaction,
        IReadOnlyList<LedgerEntry> entries,
        CancellationToken cancellationToken);

    /// <summary>The lots of a contributor, the oldest first.</summary>
    Task<IReadOnlyList<CreditLot>> ListLotsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes a new lot.</summary>
    Task AddLotAsync(CreditLot lot, CancellationToken cancellationToken);

    /// <summary>
    /// The pending lot of a contributor for a task. It is null when the
    /// contributor never had pending credits for the task.
    /// </summary>
    Task<CreditLot?> FindPendingLotAsync(
        Guid userId,
        string reference,
        CancellationToken cancellationToken);

    /// <summary>Saves the changed amounts of a lot.</summary>
    Task SaveLotAsync(CreditLot lot, CancellationToken cancellationToken);

    /// <summary>The claim of a provider subject. Null when the subject never claimed.</summary>
    Task<GrantClaim?> FindGrantClaimAsync(string provider, string subject, CancellationToken cancellationToken);

    /// <summary>Writes the claim of a provider subject.</summary>
    Task AddGrantClaimAsync(GrantClaim claim, CancellationToken cancellationToken);

    /// <summary>The credits the platform issued since the given moment.</summary>
    Task<long> SumIssuedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>The number of claims one address made since the given moment.</summary>
    Task<int> CountClaimsSinceAsync(string address, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// The transactions that moved the credits of the accounts, in the stable
    /// descending order of the list envelope.
    /// </summary>
    Task<IReadOnlyList<LedgerTransaction>> ListTransactionsAsync(
        IReadOnlyList<Guid> accountIds,
        int limit,
        CreditCursor? cursor,
        CancellationToken cancellationToken);

    /// <summary>
    /// The entries of the transactions that belong to the accounts. The entries
    /// of another wallet are never part of the answer.
    /// </summary>
    Task<IReadOnlyList<LedgerEntry>> ListEntriesAsync(
        IReadOnlyList<Guid> transactionIds,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the failure is a serialization failure or a deadlock of the
    /// database. Such a command may be retried with the same key and payload.
    /// </summary>
    bool IsRetryableConflict(Exception exception);
}
