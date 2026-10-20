namespace Blindmesh.Domain.Findings;

/// <summary>
/// The lifecycle state of a submission. The state machine of a submission is
/// its own: it never shares a state with a task, a slot, a participation, a
/// finding or a dispute. A disputed completion stays disputed until the
/// arbitration of another package resolves it.
/// </summary>
public enum SubmissionState
{
    /// <summary>The evidence is written but not submitted yet.</summary>
    Draft,

    /// <summary>The evidence is submitted and waits for the review of the owner.</summary>
    Submitted,

    /// <summary>The owner asked for one correction. The contributor may answer it.</summary>
    ChangesRequested,

    /// <summary>The owner accepted the work and the credits are settled.</summary>
    Accepted,

    /// <summary>The owner and the contributor disagree. The funds wait for the arbitration.</summary>
    Disputed,
}
