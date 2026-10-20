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

    /// <summary>The admission of the task is closed. The unused slots are refunded.</summary>
    Cancelled,
}
