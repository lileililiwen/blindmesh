namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The editable content of a task. It holds no credential and no secret: the
/// address of a resource is named, the password of a resource is not.
/// </summary>
public sealed class TaskInput
{
    /// <summary>The project the task belongs to. It is set when the task is created.</summary>
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public TaskMode Modes { get; set; }

    public TaskVisibility Visibility { get; set; } = TaskVisibility.Private;

    public int ExpectedMinutes { get; set; }

    public long BaseReward { get; set; }

    public long SlotBonus { get; set; }

    public int SlotCount { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    public bool AcceptsCleanFindings { get; set; }

    public TaskAccessKind? AccessKind { get; set; }

    public string? AccessResource { get; set; }

    public string? ReviewCriteria { get; set; }

    public IReadOnlyList<TaskRequirementInput> Requirements { get; set; } = [];

    public IReadOnlyList<TaskStepInput> Steps { get; set; } = [];
}

/// <summary>One hard requirement of a task.</summary>
public sealed record TaskRequirementInput(string Kind, string Value);

/// <summary>One step of the verification of a task.</summary>
public sealed record TaskStepInput(string Title, string ExpectedResult);
