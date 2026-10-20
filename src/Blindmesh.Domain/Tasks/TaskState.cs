namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The lifecycle state of a task. The state machine of a task is its own: it
/// never shares a state with a project, a capability, a participation, a
/// submission, a finding or a dispute. This package owns the publication part
/// of the machine, from the draft to the open task and to the cancelled task.
/// The execution states belong to the packages that own the participations.
/// </summary>
public enum TaskState
{
    /// <summary>The task is written but not published. It holds no credits.</summary>
    Draft,

    /// <summary>The task is published and accepts work. Its maximum budget is reserved.</summary>
    Open,

    /// <summary>The admission of the task is closed. The unused slots are refunded.</summary>
    Cancelled,
}
