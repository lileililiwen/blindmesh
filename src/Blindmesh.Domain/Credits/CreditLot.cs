namespace Blindmesh.Domain.Credits;

/// <summary>
/// A lot of credits of one contributor. The lot is the cached view of the
/// journal: it knows how many credits of it are spendable, how many are
/// reserved for a task and how many are owed to the contributor, and it keeps
/// the expiry and the origin of the credits.
/// </summary>
public sealed class CreditLot
{
    public Guid Id { get; set; }

    /// <summary>The owner of the lot.</summary>
    public Guid UserId { get; set; }

    /// <summary>The kind of the lot. A grant lot expires, an earned lot never does.</summary>
    public CreditLotKind Kind { get; set; }

    /// <summary>
    /// The origin of the credits, for example grant, settlement or refund. It
    /// keeps the provenance of the credits readable.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The task the lot belongs to. It is empty for the lots that hold
    /// spendable credits and it carries the reference of the task for the
    /// credits that wait in the pending bucket.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>The amount the lot was created with.</summary>
    public long OriginalAmount { get; set; }

    /// <summary>The credits of the lot that are spendable right now.</summary>
    public long AvailableAmount { get; set; }

    /// <summary>The credits of the lot that are reserved for a task.</summary>
    public long ReservedAmount { get; set; }

    /// <summary>The credits of the lot that are owed to the contributor.</summary>
    public long PendingAmount { get; set; }

    /// <summary>
    /// The moment the credits of the lot expire. It is empty for an earned
    /// lot, because earned credits never expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
