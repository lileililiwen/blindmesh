using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The actor of a task command. A visitor without a session is an anonymous
/// actor: it may read the public tasks and nothing else.
/// </summary>
public sealed record TaskActor(Guid? UserId, string Username)
{
    public static TaskActor Anonymous { get; } = new(null, string.Empty);

    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>One hard requirement of a task in an answer.</summary>
public sealed record TaskRequirementView(string Kind, string Value);

/// <summary>One step of the verification of a task in an answer.</summary>
public sealed record TaskStepView(int Position, string Title, string ExpectedResult);

/// <summary>One slot of a task in an answer.</summary>
public sealed record TaskSlotView(Guid Id, int Position, long Reward, string State, DateTimeOffset? StartedAt);

/// <summary>One applied transition of the lifecycle of a task.</summary>
public sealed record TaskStatusChangeView(string FromStatus, string ToStatus, string ChangedBy, DateTimeOffset ChangedAt);

/// <summary>
/// The summary of a task. It never carries the scope, the steps or the
/// instructions of an access, so a candidate never reads a secret of the
/// publisher through the list of the marketplace.
/// </summary>
public sealed record TaskSummaryView(
    Guid Id,
    string Title,
    IReadOnlyList<string> Modes,
    string Visibility,
    string Status,
    int ExpectedMinutes,
    long BaseReward,
    long SlotBonus,
    int SlotCount,
    long MaximumBudget,
    DateTimeOffset? Deadline,
    Guid ProjectId,
    string ProjectName,
    string OwnerUsername,
    IReadOnlyList<TaskRequirementView> Requirements,
    DateTimeOffset CreatedAt);

/// <summary>
/// The committed state of a task. The scope, the steps and the acceptance rules
/// are empty when the actor may only read the summary of the task.
/// </summary>
public sealed record TaskDetailView(
    Guid Id,
    string Title,
    string Scope,
    IReadOnlyList<string> Modes,
    string Visibility,
    string Status,
    int ExpectedMinutes,
    long BaseReward,
    long SlotBonus,
    int SlotCount,
    long MaximumBudget,
    long ReservedBudget,
    DateTimeOffset? Deadline,
    bool AcceptsCleanFindings,
    string? AccessKind,
    string? AccessResource,
    string? ReviewCriteria,
    Guid ProjectId,
    string ProjectName,
    string OwnerUsername,
    int Version,
    bool SummaryOnly,
    bool CanManage,
    IReadOnlyList<TaskRequirementView> Requirements,
    IReadOnlyList<TaskStepView> Steps,
    IReadOnlyList<TaskSlotView> Slots,
    IReadOnlyList<TaskStatusChangeView> StatusHistory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? CancelledAt);

/// <summary>The outcome of a task command.</summary>
public enum TaskCommandStatus
{
    /// <summary>The command changed the task.</summary>
    Succeeded,

    /// <summary>The command created the task.</summary>
    Created,

    /// <summary>The task does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The task is visible but the actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>The expected version of the task is not the current one.</summary>
    VersionConflict,

    /// <summary>The requested transition is not allowed by the state machine.</summary>
    InvalidTransition,

    /// <summary>The state of the task does not accept the command.</summary>
    StateConflict,

    /// <summary>The account of the publisher does not hold the budget of the task.</summary>
    InsufficientFunds,
}

/// <summary>
/// The answer of a task command. The current version is part of the answer of a
/// version conflict, so the caller can reload the task.
/// </summary>
public sealed record TaskCommandResult(
    TaskCommandStatus Status,
    TaskDetailView? Task = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    int? CurrentVersion = null,
    string? Message = null);

/// <summary>The query of the list of the tasks.</summary>
public sealed record TaskListQuery(
    int Limit,
    TaskCursor? Cursor,
    TaskMode? Mode,
    string? RequirementKind,
    string? RequirementValue,
    Guid? ProjectId);

/// <summary>One page of the list of the tasks.</summary>
public sealed record TaskPage(IReadOnlyList<TaskPageItem> Items, TaskCursor? Next);

/// <summary>
/// One task of a page of the list. The flag tells the service that the row
/// belongs to the matched scope, so the service confirms it with the rule of
/// the scope before it shows it. A row of the public scope needs no
/// confirmation.
/// </summary>
public sealed record TaskPageItem(MarketplaceTask Task, bool RequiresMatch);
