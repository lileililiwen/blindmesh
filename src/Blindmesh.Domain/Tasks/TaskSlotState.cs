namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The state of a slot of a task. A slot holds one part of the reserved budget
/// of the task. An unused slot is refunded when the task is cancelled; a slot
/// that carries running, submitted or disputed work keeps its budget.
/// </summary>
public enum TaskSlotState
{
    /// <summary>The slot is free and holds its part of the reserved budget.</summary>
    Available,

    /// <summary>
    /// The slot is held by one accepted contributor that has not started the
    /// work yet. The slot keeps its part of the reserved budget and the
    /// contributor has to start within the start window.
    /// </summary>
    Leased,

    /// <summary>A contributor started the work of the slot.</summary>
    Active,

    /// <summary>The work of the slot is submitted and waits for the review.</summary>
    Submitted,

    /// <summary>The work of the slot is disputed and keeps its budget.</summary>
    Disputed,

    /// <summary>The work of the slot is accepted and its budget is paid out.</summary>
    Settled,

    /// <summary>The slot was refunded because the task was cancelled.</summary>
    Refunded,
}
