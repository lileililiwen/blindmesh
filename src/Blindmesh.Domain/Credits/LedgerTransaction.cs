namespace Blindmesh.Domain.Credits;

/// <summary>
/// A transaction of the credit ledger. It groups the entries that move the
/// credits at the same moment. The signed sum of the entries of a transaction
/// is always zero, and a transaction is never changed after it is written.
/// </summary>
public sealed class LedgerTransaction
{
    public Guid Id { get; set; }

    /// <summary>The kind of the transaction, for example grant or reserve.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// What the transaction is about. The grant carries the constant grant
    /// reference, an escrow carries the reference of the task.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>The contributor that caused the transaction.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>
    /// The idempotency key of the command that caused the transaction. It is
    /// kept with the transaction, so the audit trail of a money command stays
    /// readable.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>An optional note of the transaction. It never holds a secret.</summary>
    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
