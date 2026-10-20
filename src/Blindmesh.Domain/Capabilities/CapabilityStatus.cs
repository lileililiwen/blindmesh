namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// The lifecycle state of a capability. The state machine of a capability is its
/// own: it never shares a state with a task, a participation, a submission, a
/// finding or a dispute. A withdrawn capability keeps its row and its history.
/// </summary>
public enum CapabilityStatus
{
    /// <summary>The declaration is offered by its owner.</summary>
    Declared,

    /// <summary>
    /// The owner withdrew the declaration. The row and its history stay, but
    /// the capability never matches a condition again.
    /// </summary>
    Withdrawn,
}
