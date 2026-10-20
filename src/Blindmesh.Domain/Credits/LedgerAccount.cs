namespace Blindmesh.Domain.Credits;

/// <summary>
/// A bucket of the credit ledger. The account of a contributor holds the
/// spendable credits, the reserved credits of the tasks he published and the
/// credits that are owed to him. The platform accounts hold the issued supply
/// and the sink of the expired credits. A row of this table is never updated
/// after it is created, because the journal is the truth.
/// </summary>
public sealed class LedgerAccount
{
    public Guid Id { get; set; }

    /// <summary>The bucket of the account.</summary>
    public LedgerBucket Bucket { get; set; }

    /// <summary>The owner of the account. It is empty for the platform accounts.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// The reference of the account. The ordinary bucket of a contributor has
    /// an empty reference, the escrow buckets carry the reference of the task
    /// and the platform accounts carry the constant platform reference.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
