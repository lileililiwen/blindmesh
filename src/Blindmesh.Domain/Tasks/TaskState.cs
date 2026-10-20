namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The lifecycle state of a task. The state machine of a task is its own: it
/// never shares a state with a project, a capability, a participation, a
/// submission, a finding or a dispute. This package owns the publication part
/// of the machine, from the draft to the open task and to the cancelled task.
/// The execution state belongs to the package that owns the participations.
/// </summary>
public enum TaskState
{
    /// <summary>The task is written but not published. It holds no credits.</summary>
    Draft,

    /// <summary>The task is published and accepts work. Its maximum budget is reserved.</summary>
    Open,

    /// <summary>
    /// The task carries running work. The admission stays open and the budget
    /// of the running participations is kept, so a task with work in flight
    /// stays visible in the marketplace.
    /// </summary>
    InProgress,

    /// <summary>
    /// The task waits for the review of a submission or for the arbitration of
    /// a dispute. The admission stays open, because the slots that are still
    /// free may be filled.
    /// </summary>
    Review,

    /// <summary>
    /// Every slot of the task is settled or refunded. The escrow of the task is
    /// empty and the task accepts no work anymore.
    /// </summary>
    Completed,

    /// <summary>The admission of the task is closed. The unused slots are refunded.</summary>
    Cancelled,
}
