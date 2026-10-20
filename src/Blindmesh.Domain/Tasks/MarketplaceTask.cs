using System.ComponentModel.DataAnnotations;

namespace Blindmesh.Domain.Tasks;

/// <summary>
/// A task of the marketplace. The row holds the scope, the modes, the
/// acceptance rules, the deadline, the slots and the maximum budget. A task
/// belongs to a project and keeps the visibility of that project as its outer
/// limit. A task is never deleted; it is cancelled and keeps its history.
/// </summary>
public sealed class MarketplaceTask
{
    public Guid Id { get; set; }

    /// <summary>The project the task belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The contributor that wrote the task and that reserves its budget.</summary>
    public Guid OwnerId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The description of the work. It holds no credential and no secret; the
    /// address of a resource is named, the password of a resource is not.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The nonempty set of the modes of the task.</summary>
    public TaskMode Modes { get; set; }

    public TaskVisibility Visibility { get; set; } = TaskVisibility.Private;

    public TaskState Status { get; set; } = TaskState.Draft;

    /// <summary>The expected minutes of the work of one slot.</summary>
    public int ExpectedMinutes { get; set; }

    /// <summary>The base reward of one slot.</summary>
    public long BaseReward { get; set; }

    /// <summary>The bonus that one slot may earn on top of its base reward.</summary>
    public long SlotBonus { get; set; }

    /// <summary>The number of slots of the task.</summary>
    public int SlotCount { get; set; }

    /// <summary>The moment the work has to be finished.</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>
    /// True when a clean exploration is a valid result of the task. An explore
    /// task accepts a report that holds no finding.
    /// </summary>
    public bool AcceptsCleanFindings { get; set; }

    /// <summary>The kind of the access the task needs. Null when the task carries no access mode.</summary>
    public TaskAccessKind? AccessKind { get; set; }

    /// <summary>
    /// The resource the owner operates or the reference of the delegated
    /// consent. It never holds a credential.
    /// </summary>
    public string? AccessResource { get; set; }

    /// <summary>
    /// The structure of the peer assessment a review task accepts. It is empty
    /// when the task carries no review mode.
    /// </summary>
    public string? ReviewCriteria { get; set; }

    /// <summary>The maximum budget that was reserved when the task was published.</summary>
    public long ReservedBudget { get; set; }

    /// <summary>The optimistic concurrency version of the task.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}

/// <summary>
/// A hard requirement of a task. The requirement names the kind of capability
/// the task needs and the value it has to match. A task never requires a
/// credential of a resource, so the kinds are the ones that a contributor
/// offers: a region, a device or a role.
/// </summary>
public sealed class TaskRequirement
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    /// <summary>The kind of the requirement: region, device or role.</summary>
    [MaxLength(16)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The value the declaration of the contributor has to match.</summary>
    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// One step of a verification. A step names what the contributor does and the
/// result that is expected, so a verify task always carries both.
/// </summary>
public sealed class TaskStep
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    /// <summary>The position of the step in the task. The steps are read in this order.</summary>
    public int Position { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string ExpectedResult { get; set; } = string.Empty;
}

/// <summary>
/// One slot of a task. A slot holds the part of the reserved budget that pays
/// one contributor. The slots of a task are created when the task is published.
/// </summary>
public sealed class TaskSlot
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    /// <summary>The position of the slot in the task.</summary>
    public int Position { get; set; }

    /// <summary>The credits the slot holds. It is the base reward plus the bonus.</summary>
    public long Reward { get; set; }

    public TaskSlotState State { get; set; } = TaskSlotState.Available;

    /// <summary>The contributor that holds the slot. Null while the slot is free.</summary>
    public Guid? ContributorId { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
}

/// <summary>
/// One applied transition of the lifecycle of a task. The history is written
/// in the same transaction as the task, so it never misses a state.
/// </summary>
public sealed class TaskStatusChange
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    [MaxLength(16)]
    public string FromStatus { get; set; } = string.Empty;

    [MaxLength(16)]
    public string ToStatus { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
