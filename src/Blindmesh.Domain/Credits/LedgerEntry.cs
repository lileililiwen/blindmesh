namespace Blindmesh.Domain.Credits;

/// <summary>
/// One entry of the credit journal. An entry never changes after it is
/// written: a correction is a compensating transaction with its own entries.
/// The signed amount is positive when the bucket grows and negative when it
/// shrinks, and the signed sum of the entries of a transaction is zero.
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>The signed amount of the entry in credits.</summary>
    public long Amount { get; set; }

    /// <summary>
    /// The lot the entry belongs to. It keeps the provenance of a split
    /// reservation readable. It is empty for the platform accounts.
    /// </summary>
    public Guid? LotId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
