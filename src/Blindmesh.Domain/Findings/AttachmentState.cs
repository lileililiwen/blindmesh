namespace Blindmesh.Domain.Findings;

/// <summary>
/// The state of an attachment. An attachment is private and quarantined: it is
/// born quarantined and it is never readable while it is quarantined. The
/// check of the file releases it, a refused file stays refused.
/// </summary>
public enum AttachmentState
{
    /// <summary>The file waits for its check. It is never readable in this state.</summary>
    Quarantined,

    /// <summary>The file passed the check of the pipeline and may be read.</summary>
    Scanned,

    /// <summary>The file was refused by the check of the pipeline.</summary>
    Rejected,
}
