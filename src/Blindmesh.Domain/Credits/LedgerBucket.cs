namespace Blindmesh.Domain.Credits;

/// <summary>
/// The buckets of the credit ledger. Every entry of a transaction belongs to
/// exactly one bucket, and the signed sum of the entries of a transaction is
/// zero. The ordinary bucket of a contributor never holds a negative amount.
/// </summary>
public enum LedgerBucket
{
    /// <summary>The spendable credits of a contributor.</summary>
    UserAvailable,

    /// <summary>The credits a publisher reserved for a task.</summary>
    Reserved,

    /// <summary>The credits that are owed to a contributor and not paid out yet.</summary>
    Pending,

    /// <summary>The supply the platform issued. It is the only source of credits.</summary>
    PlatformIssuance,

    /// <summary>The sink of the credits that expired. It never pays out.</summary>
    PlatformExpiry,
}
