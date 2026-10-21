namespace Blindmesh.Domain.Moderation;

/// <summary>
/// The lifecycle state of a dispute. The state machine of a dispute is its own:
/// it never shares a state with a task, a slot, a participation, a submission,
/// a finding or a report.
/// </summary>
public enum DisputeState
{
    /// <summary>The dispute is open and waits for a moderator.</summary>
    Open,

    /// <summary>A moderator asked the parties for more evidence.</summary>
    EvidenceRequested,

    /// <summary>A moderator resolved the dispute and the escrow was moved.</summary>
    Resolved,

    /// <summary>The case is over and the window of the appeal has passed.</summary>
    Closed,
}
