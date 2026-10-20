using System.ComponentModel.DataAnnotations;

namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The seat of one contributor in one slot of a task. A participation is
/// created by an application and holds the state of the seat: an applied
/// participation holds no slot, an accepted participation holds one exclusive
/// lease and a running participation holds the budget of its slot.
/// </summary>
public sealed class Participation
{
    public Guid Id { get; set; }

    /// <summary>The task the participation belongs to.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The application that asked for the seat.</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>The contributor that holds the seat.</summary>
    public Guid ContributorId { get; set; }

    public ParticipationState State { get; set; } = ParticipationState.Applied;

    /// <summary>The slot of the task. Null while the participation holds no slot.</summary>
    public Guid? SlotId { get; set; }

    /// <summary>The lease of the slot. Null while the participation holds no slot.</summary>
    public Guid? LeaseId { get; set; }

    /// <summary>The moment the contributor applied.</summary>
    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>The moment the owner accepted the application.</summary>
    public DateTimeOffset? AcceptedAt { get; set; }

    /// <summary>The moment the contributor started the work.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// The visible deadline of the execution. It is the smaller of the
    /// deadline of the task and the start of the work plus seven days.
    /// </summary>
    public DateTimeOffset? ExecutionDeadline { get; set; }

    /// <summary>The moment the participation reached a state it never leaves.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>The optimistic concurrency version of the participation.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// The application of one contributor for one slot of a task. The application
/// keeps the snapshot of the capability evaluation, the consent of the scope
/// and the version of the task the contributor saw. It is written once and is
/// never changed, so the seat and the evidence of the seat never disagree.
/// </summary>
public sealed class TaskApplication
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public Guid ContributorId { get; set; }

    /// <summary>
    /// The snapshot of the evaluation of the hard conditions of the task at the
    /// moment of the application. It holds no credential and no secret.
    /// </summary>
    public string CapabilitySnapshot { get; set; } = string.Empty;

    /// <summary>
    /// The statement of the contributor that it accepts the scope of the task.
    /// It is a statement and not a credential.
    /// </summary>
    public bool ScopeConsent { get; set; }

    /// <summary>The version of the task the contributor saw.</summary>
    public int ExpectedVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// The exclusive lease of one slot. Only one live lease may exist for a slot,
/// so two owners cannot hold the same seat. The lease is released when the
/// seat is withdrawn or when it expires.
/// </summary>
public sealed class SlotLease
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    /// <summary>The slot the lease holds. Only one live lease may exist for it.</summary>
    public Guid SlotId { get; set; }

    public Guid ParticipationId { get; set; }

    public Guid ContributorId { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>The moment the lease was released. Null while the lease is live.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }
}

/// <summary>
/// One applied transition of the lifecycle of a participation. The history is
/// written in the same transaction as the participation, so it never misses a
/// state.
/// </summary>
public sealed class ParticipationStatusChange
{
    public Guid Id { get; set; }

    public Guid ParticipationId { get; set; }

    public Guid TaskId { get; set; }

    [MaxLength(32)]
    public string FromStatus { get; set; } = string.Empty;

    [MaxLength(32)]
    public string ToStatus { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
