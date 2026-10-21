namespace Blindmesh.Domain.Moderation;

/// <summary>
/// The lifecycle state of a report. The state machine of a report is its own:
/// it never shares a state with a task, a slot, a participation, a submission,
/// a finding or a dispute.
/// </summary>
public enum ReportState
{
    /// <summary>The report was written and waits for a moderator.</summary>
    Open,

    /// <summary>A moderator reads the report.</summary>
    UnderReview,

    /// <summary>The report was answered and the case is over.</summary>
    Resolved,

    /// <summary>The report was refused and the case is over.</summary>
    Dismissed,
}
