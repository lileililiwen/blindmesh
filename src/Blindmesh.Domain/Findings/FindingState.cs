namespace Blindmesh.Domain.Findings;

/// <summary>
/// The state of a finding. A finding is classified on its own: the decision of
/// a finding never decides the earned base pay of a submission.
/// </summary>
public enum FindingState
{
    /// <summary>The contributor reported the finding and it waits for the triage.</summary>
    Submitted,

    /// <summary>The owner read the finding and started to classify it.</summary>
    Triaged,

    /// <summary>The owner confirmed the finding.</summary>
    Confirmed,

    /// <summary>The owner rejected the finding. The pay of a completed scope is not touched.</summary>
    Rejected,

    /// <summary>The owner marked the finding as a duplicate of another one.</summary>
    Duplicate,

    /// <summary>The owner works on the confirmed finding.</summary>
    Fixing,

    /// <summary>The confirmed finding is closed.</summary>
    Resolved,
}
