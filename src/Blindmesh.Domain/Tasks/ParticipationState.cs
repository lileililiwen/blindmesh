namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The lifecycle state of a participation. The state machine of a participation
/// is its own: it never shares a state with a task, a slot, a submission, a
/// finding or a dispute. An applied participation holds no slot; an accepted
/// participation holds exactly one exclusive lease.
/// </summary>
public enum ParticipationState
{
    /// <summary>The contributor applied. The application holds no slot yet.</summary>
    Applied,

    /// <summary>
    /// The owner accepted the application. The participation holds one
    /// exclusive lease and the contributor has to start within the start window.
    /// </summary>
    Accepted,

    /// <summary>The contributor started the work. The execution deadline is visible.</summary>
    Running,

    /// <summary>
    /// The contributor sent the evidence of the seat. The submission package
    /// owns this state, because it owns the evidence.
    /// </summary>
    Submitted,

    /// <summary>
    /// The owner asked for the one correction of the seat. The submission
    /// package owns this state, because it owns the evidence.
    /// </summary>
    ChangesRequested,

    /// <summary>
    /// The owner accepted the work of the seat and the credits are settled. The
    /// submission package owns this state, because it owns the settlement.
    /// </summary>
    Approved,

    /// <summary>
    /// The owner and the contributor disagree about the completion. The funds
    /// wait for the arbitration of another package.
    /// </summary>
    Disputed,

    /// <summary>The owner declined the application. It never held a slot.</summary>
    Declined,

    /// <summary>The contributor withdrew before the work started.</summary>
    Withdrawn,

    /// <summary>A deadline of the participation passed.</summary>
    Expired,
}
