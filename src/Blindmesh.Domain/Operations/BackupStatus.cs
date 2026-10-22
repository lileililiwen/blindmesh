namespace Blindmesh.Domain.Operations;

/// <summary>
/// The state of one backup of the product. A backup is only usable when it was
/// verified against its own checksum.
/// </summary>
public enum BackupStatus
{
    /// <summary>The backup was taken and is not verified yet.</summary>
    Started,

    /// <summary>The checksum of the backup was read back and it matches.</summary>
    Verified,

    /// <summary>The backup failed and it may not be restored.</summary>
    Failed,
}
