namespace Blindmesh.Domain.Operations;

/// <summary>
/// The state of one restore rehearsal. A rehearsal that did not pass every
/// check blocks the promotion of the backup it rehearsed.
/// </summary>
public enum RestoreRehearsalStatus
{
    /// <summary>The rehearsal runs right now.</summary>
    Started,

    /// <summary>Every check of the rehearsal passed.</summary>
    Passed,

    /// <summary>At least one check of the rehearsal failed.</summary>
    Failed,
}
