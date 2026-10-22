namespace Blindmesh.Domain.Operations;

/// <summary>
/// A backup the operator took. The row holds the facts the restore rehearsal
/// needs: when the backup was taken, what it holds, how large it is, the
/// checksum of its content and the day it may be deleted.
///
/// The detail is written by the operator and it is read back to the operator
/// only. It never holds a credential, a password or a path that leaks one.
/// </summary>
public sealed class BackupRecord
{
    public Guid Id { get; set; }

    /// <summary>The moment the backup was taken.</summary>
    public DateTimeOffset TakenAt { get; set; }

    /// <summary>What the backup holds.</summary>
    public BackupKind Kind { get; set; }

    /// <summary>The place of the backup, for example the name of a bucket.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>The size of the backup in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>The checksum of the content of the backup, in lower case hex.</summary>
    public string ChecksumSha256 { get; set; } = string.Empty;

    /// <summary>The state of the backup.</summary>
    public BackupStatus Status { get; set; }

    /// <summary>The day the backup may be deleted.</summary>
    public DateTimeOffset RetentionUntil { get; set; }

    /// <summary>A redacted note of the operator about the backup.</summary>
    public string Detail { get; set; } = string.Empty;
}
