using Blindmesh.Application.Contracts;

namespace Blindmesh.Application.Credits;

/// <summary>
/// The actor of a credit command. A visitor without a session is an anonymous
/// actor: it may read nothing of the wallet, because a wallet is private.
/// </summary>
public sealed record CreditActor(Guid? UserId, string Username)
{
    public static CreditActor Anonymous { get; } = new(null, string.Empty);

    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>One lot of credits with its expiry and its origin.</summary>
public sealed record CreditLotView(
    Guid Id,
    string Kind,
    string Source,
    long OriginalAmount,
    long AvailableAmount,
    long ReservedAmount,
    long PendingAmount,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    bool Expired);

/// <summary>The change of one bucket of a transaction.</summary>
public sealed record CreditBalanceChangeView(string Bucket, long Amount);

/// <summary>One entry of the journal that belongs to the wallet of the caller.</summary>
public sealed record CreditEntryView(
    Guid Id,
    string Bucket,
    long Amount,
    Guid? LotId,
    DateTimeOffset CreatedAt);

/// <summary>One transaction of the wallet history.</summary>
public sealed record CreditTransactionView(
    Guid Id,
    string Kind,
    string Reference,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CreditBalanceChangeView> Changes,
    IReadOnlyList<CreditEntryView> Entries);

/// <summary>
/// The state of the limited grant for one contributor. It holds the decision
/// and the reason of every unmet condition, so the wallet can explain itself.
/// </summary>
public sealed record CreditGrantView(
    bool Eligible,
    long Amount,
    int ExpiresInDays,
    int MinAccountAgeDays,
    IReadOnlyList<string> Reasons,
    DateTimeOffset? ClaimedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// The wallet of a contributor. The available, the reserved and the pending
/// balance are separate and are each rebuildable from the journal.
/// </summary>
public sealed record CreditWalletView(
    Guid UserId,
    long Available,
    long Reserved,
    long Pending,
    IReadOnlyList<CreditLotView> Lots,
    CreditGrantView Grant,
    DateTimeOffset AsOf);

/// <summary>
/// The page of the wallet history. It holds the list envelope of the product,
/// so the items and the cursor of the next page are read the same way as in
/// every other list.
/// </summary>
public sealed record CreditTransactionPage(
    IReadOnlyList<CreditTransactionView> Items,
    string? NextCursor);

/// <summary>The outcome of a credit command.</summary>
public enum CreditCommandStatus
{
    /// <summary>The command changed the ledger.</summary>
    Succeeded,

    /// <summary>The wallet or the account does not exist or may not be seen.</summary>
    NotFound,

    /// <summary>The actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>
    /// The state of the ledger refused the command. A reservation that would
    /// overspend the account and a subject that claimed the grant already are
    /// conflicts.
    /// </summary>
    Conflict,

    /// <summary>
    /// The daily limit of the platform or of the address is used up, so the
    /// command has to wait for the next day.
    /// </summary>
    LimitReached,
}

/// <summary>
/// The answer of a credit command. It never carries an amount of a wallet the
/// actor may not see.
/// </summary>
public sealed record CreditCommandResult(
    CreditCommandStatus Status,
    CreditWalletView? Wallet = null,
    CreditTransactionView? Transaction = null,
    IReadOnlyList<string>? Reasons = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Message = null);

/// <summary>
/// The command that claims the limited grant of a contributor. The address of
/// the caller is only kept to hold the daily limit of the claims of one
/// address; it is never part of an answer.
/// </summary>
public sealed record CreditGrantCommand(
    bool RulesAccepted,
    string? IdempotencyKey = null,
    string? IpAddress = null);

/// <summary>The command that reserves credits of a publisher for a task.</summary>
public sealed record CreditReserveCommand(long Amount, string Reference, string? IdempotencyKey = null);

/// <summary>
/// The command that moves reserved credits of a publisher into the pending
/// bucket of the contributor that started the work.
/// </summary>
public sealed record CreditPendingCommand(
    string Reference,
    Guid ContributorId,
    long Amount,
    string? IdempotencyKey = null);

/// <summary>The command that pays the pending credits of a contributor out.</summary>
public sealed record CreditSettleCommand(string Reference, long Amount, string? IdempotencyKey = null);

/// <summary>
/// The command that returns the reserved credits of a publisher. An expired
/// grant goes to the expiry sink instead of back to the publisher.
/// </summary>
public sealed record CreditRefundCommand(string Reference, long Amount, string? IdempotencyKey = null);
