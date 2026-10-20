using System.Globalization;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Credits;
using Blindmesh.Application.Projects;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The service of the tasks module. It owns the task model, the modes, the
/// acceptance rules, the publication with its escrow and the bounded search of
/// the marketplace. The actor policy is checked again on every command, so a
/// change of the membership or of the visibility of the parent project is
/// honoured by the next read.
/// </summary>
public sealed class TaskService(
    ITaskStore store,
    ProjectService projects,
    ICreditService credits,
    IClock clock)
{
    /// <summary>
    /// The tasks the actor may see, in the stable descending order of the list
    /// envelope. The answer carries summaries only, so a candidate never reads
    /// the scope or the instructions of an access of a task.
    /// </summary>
    public async Task<PagedResult<TaskSummaryView>> ListAsync(
        TaskActor actor,
        TaskListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);

        var page = await store.ListVisibleAsync(actor.UserId, query, cancellationToken);

        if (page.Tasks.Count == 0)
        {
            return new PagedResult<TaskSummaryView>
            {
                Items = [],
                NextCursor = page.Next?.Encode(),
            };
        }

        var taskIds = page.Tasks.Select(task => task.Id).ToArray();
        var projectIds = page.Tasks.Select(task => task.ProjectId).Distinct().ToArray();
        var ownerIds = page.Tasks.Select(task => task.OwnerId).Distinct().ToArray();

        var requirements = await store.ListRequirementsAsync(taskIds, cancellationToken);
        var projectNames = await store.ListProjectNamesAsync(projectIds, cancellationToken);
        var usernames = await store.ListUsernamesAsync(ownerIds, cancellationToken);

        var items = page.Tasks
            .Select(task => new TaskSummaryView(
                task.Id,
                task.Title,
                ModeNames(task.Modes),
                VisibilityText(task.Visibility),
                StatusText(task.Status),
                task.ExpectedMinutes,
                task.BaseReward,
                task.SlotBonus,
                task.SlotCount,
                TaskRules.MaximumBudget(task.BaseReward, task.SlotBonus, task.SlotCount),
                task.Deadline,
                task.ProjectId,
                projectNames.GetValueOrDefault(task.ProjectId, string.Empty),
                usernames.GetValueOrDefault(task.OwnerId, string.Empty),
                requirements
                    .GetValueOrDefault(task.Id, [])
                    .Select(requirement => new TaskRequirementView(requirement.Kind, requirement.Value))
                    .ToArray(),
                task.CreatedAt))
            .ToArray();

        return new PagedResult<TaskSummaryView>
        {
            Items = items,
            NextCursor = page.Next?.Encode(),
        };
    }

    /// <summary>The committed state of a task.</summary>
    public async Task<TaskCommandResult> GetAsync(
        TaskActor actor,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var access = await LoadAsync(actor, taskId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        return new TaskCommandResult(
            TaskCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Writes a task in the draft state. The task holds no credits before it is
    /// published.
    /// </summary>
    public async Task<TaskCommandResult> CreateAsync(
        TaskActor actor,
        TaskInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Sign in to write a task.");
        }

        var projectAccess = await projects.GetAccessAsync(
            ToProjectActor(actor),
            input.ProjectId,
            cancellationToken);

        if (projectAccess is null)
        {
            return NotFound();
        }

        if (!projectAccess.CanEdit)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Only the owner and the editors of the project may write a task.");
        }

        var errors = TaskRules.Validate(input);

        if (errors.Count > 0)
        {
            return new TaskCommandResult(TaskCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var now = clock.UtcNow;
        var task = new MarketplaceTask
        {
            Id = Guid.NewGuid(),
            ProjectId = input.ProjectId,
            OwnerId = actor.UserId!.Value,
            Title = input.Title.Trim(),
            Scope = input.Scope.Trim(),
            Modes = input.Modes,
            Visibility = input.Visibility,
            Status = TaskState.Draft,
            ExpectedMinutes = input.ExpectedMinutes,
            BaseReward = input.BaseReward,
            SlotBonus = input.SlotBonus,
            SlotCount = input.SlotCount,
            Deadline = input.Deadline,
            AcceptsCleanFindings = input.AcceptsCleanFindings,
            AccessKind = input.Modes.HasFlag(TaskMode.Access) ? input.AccessKind : null,
            AccessResource = input.Modes.HasFlag(TaskMode.Access)
                ? input.AccessResource?.Trim()
                : null,
            ReviewCriteria = input.Modes.HasFlag(TaskMode.Review)
                ? input.ReviewCriteria?.Trim()
                : null,
            ReservedBudget = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await store.AddTaskAsync(task, cancellationToken);
        await store.AddRequirementsAsync(BuildRequirements(task.Id, input), cancellationToken);
        await store.AddStepsAsync(BuildSteps(task.Id, input), cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var access = new TaskAccess(task, projectAccess, SummaryOnly: false, CanManage: true);

        return new TaskCommandResult(
            TaskCommandStatus.Created,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Changes the editable content of a task. The budget of a published task
    /// is reserved, so its reward, its bonus and its number of slots never
    /// change after the publication.
    /// </summary>
    public async Task<TaskCommandResult> UpdateAsync(
        TaskActor actor,
        Guid taskId,
        TaskInput input,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(input);

        var access = await LoadAsync(actor, taskId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (!access.CanManage)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Only the owner of the task and the owner of the project may change it.");
        }

        if (access.Task.Status == TaskState.Cancelled)
        {
            return new TaskCommandResult(
                TaskCommandStatus.StateConflict,
                Message: "The task is cancelled and does not accept changes.");
        }

        if (access.Task.Version != expectedVersion)
        {
            return Conflict(access.Task.Version);
        }

        var errors = TaskRules.Validate(input);

        if (access.Task.Status == TaskState.Open)
        {
            if (input.BaseReward != access.Task.BaseReward ||
                input.SlotBonus != access.Task.SlotBonus ||
                input.SlotCount != access.Task.SlotCount)
            {
                errors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal)
                {
                    ["base_reward"] = ["The budget of a published task is reserved and never changes."],
                };
            }
        }

        if (errors.Count > 0)
        {
            return new TaskCommandResult(TaskCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var task = access.Task;

        task.Title = input.Title.Trim();
        task.Scope = input.Scope.Trim();
        task.Modes = input.Modes;
        task.Visibility = input.Visibility;
        task.ExpectedMinutes = input.ExpectedMinutes;
        task.BaseReward = input.BaseReward;
        task.SlotBonus = input.SlotBonus;
        task.SlotCount = input.SlotCount;
        task.Deadline = input.Deadline;
        task.AcceptsCleanFindings = input.AcceptsCleanFindings;
        task.AccessKind = input.Modes.HasFlag(TaskMode.Access) ? input.AccessKind : null;
        task.AccessResource = input.Modes.HasFlag(TaskMode.Access) ? input.AccessResource?.Trim() : null;
        task.ReviewCriteria = input.Modes.HasFlag(TaskMode.Review) ? input.ReviewCriteria?.Trim() : null;
        task.Version += 1;
        task.UpdatedAt = clock.UtcNow;

        await store.ReplaceRequirementsAsync(task.Id, BuildRequirements(task.Id, input), cancellationToken);
        await store.ReplaceStepsAsync(task.Id, BuildSteps(task.Id, input), cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        return new TaskCommandResult(
            TaskCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Publishes a task and reserves its maximum budget. The reservation and
    /// the publication are written in the same transaction, so a task never
    /// becomes open without its budget and a failed publication leaves neither
    /// a task nor a reservation behind.
    /// </summary>
    public async Task<TaskCommandResult> PublishAsync(
        TaskActor actor,
        Guid taskId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsAuthenticated)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Sign in to publish a task.");
        }

        var task = await store.LockTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var projectAccess = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (projectAccess is null)
        {
            return NotFound();
        }

        if (task.OwnerId != actor.UserId!.Value)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Only the owner of the task may publish it.");
        }

        if (task.Status != TaskState.Draft)
        {
            return new TaskCommandResult(
                TaskCommandStatus.InvalidTransition,
                Message: "Only a draft task may be published.");
        }

        if (!projectAccess.IsActive)
        {
            return new TaskCommandResult(
                TaskCommandStatus.StateConflict,
                Message: "The project of the task is not active and does not accept tasks.");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var steps = (await store.ListStepsAsync([task.Id], cancellationToken))
            .GetValueOrDefault(task.Id, []);
        var requirements = (await store.ListRequirementsAsync([task.Id], cancellationToken))
            .GetValueOrDefault(task.Id, []);

        foreach (var entry in TaskRules.Validate(ToInput(task, steps, requirements)))
        {
            errors[entry.Key] = entry.Value;
        }

        foreach (var entry in TaskRules.ValidatePublication(ToInput(task, steps, requirements)))
        {
            errors[entry.Key] = entry.Value;
        }

        foreach (var entry in TaskRules.ValidateDeadline(task.Deadline, clock.UtcNow))
        {
            errors[entry.Key] = entry.Value;
        }

        if (errors.Count > 0)
        {
            return new TaskCommandResult(TaskCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var budget = TaskRules.MaximumBudget(task.BaseReward, task.SlotBonus, task.SlotCount);
        var reference = EscrowReference(task.Id);

        var reservation = await credits.ReserveTaskAsync(
            new CreditActor(actor.UserId, actor.Username),
            new CreditReserveCommand(budget, reference, idempotencyKey),
            cancellationToken);

        if (reservation.Status != CreditCommandStatus.Succeeded)
        {
            return new TaskCommandResult(
                TaskCommandStatus.InsufficientFunds,
                FieldErrors: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["budget"] =
                    [
                        "The account does not hold that many spendable credits for the budget of the task.",
                    ],
                },
                Message: reservation.Message ?? "The budget of the task could not be reserved.");
        }

        var now = clock.UtcNow;

        task.Status = TaskState.Open;
        task.ReservedBudget = budget;
        task.PublishedAt = now;
        task.UpdatedAt = now;
        task.Version += 1;

        var slots = new List<TaskSlot>(task.SlotCount);

        for (var position = 1; position <= task.SlotCount; position++)
        {
            slots.Add(new TaskSlot
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                Position = position,
                Reward = task.BaseReward + task.SlotBonus,
                State = TaskSlotState.Available,
            });
        }

        await store.AddSlotsAsync(slots, cancellationToken);
        await store.AddStatusChangeAsync(
            new TaskStatusChange
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                FromStatus = StatusText(TaskState.Draft),
                ToStatus = StatusText(TaskState.Open),
                ChangedBy = actor.UserId.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var access = new TaskAccess(task, projectAccess, SummaryOnly: false, CanManage: true);

        return new TaskCommandResult(
            TaskCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Cancels a task. The admission closes, the unused slots are refunded and
    /// the slots that carry running, submitted or disputed work keep their
    /// budget.
    /// </summary>
    public async Task<TaskCommandResult> CancelAsync(
        TaskActor actor,
        Guid taskId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsAuthenticated)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Sign in to cancel a task.");
        }

        var task = await store.LockTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var projectAccess = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (projectAccess is null)
        {
            return NotFound();
        }

        var isOwner = task.OwnerId == actor.UserId!.Value;

        if (!isOwner && !projectAccess.IsOwner)
        {
            return new TaskCommandResult(
                TaskCommandStatus.Forbidden,
                Message: "Only the owner of the task and the owner of the project may cancel it.");
        }

        if (task.Status is not (TaskState.Open or TaskState.InProgress))
        {
            return new TaskCommandResult(
                TaskCommandStatus.InvalidTransition,
                Message: "Only an open task may be cancelled.");
        }

        var slots = (await store.ListSlotsAsync([task.Id], cancellationToken))
            .GetValueOrDefault(task.Id, []);

        var unused = slots.Where(slot => slot.State == TaskSlotState.Available).ToArray();
        var refund = unused.Sum(slot => slot.Reward);

        if (refund > 0)
        {
            var usernames = await store.ListUsernamesAsync([task.OwnerId], cancellationToken);
            var publisher = new CreditActor(
                task.OwnerId,
                usernames.GetValueOrDefault(task.OwnerId, string.Empty));

            var refundResult = await credits.RefundUnstartedAsync(
                publisher,
                new CreditRefundCommand(EscrowReference(task.Id), refund, idempotencyKey),
                cancellationToken);

            if (refundResult.Status != CreditCommandStatus.Succeeded)
            {
                return new TaskCommandResult(
                    TaskCommandStatus.StateConflict,
                    Message: refundResult.Message ?? "The unused slots could not be refunded.");
            }
        }

        var now = clock.UtcNow;

        foreach (var slot in unused)
        {
            slot.State = TaskSlotState.Refunded;
        }

        task.Status = TaskState.Cancelled;
        task.CancelledAt = now;
        task.UpdatedAt = now;
        task.Version += 1;

        await store.AddStatusChangeAsync(
            new TaskStatusChange
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                FromStatus = StatusText(task.Status),
                ToStatus = StatusText(TaskState.Cancelled),
                ChangedBy = actor.UserId.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var access = new TaskAccess(task, projectAccess, SummaryOnly: false, CanManage: true);

        return new TaskCommandResult(
            TaskCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// The task and what the actor may do with it. The access is null when the
    /// task may not be seen by the actor at all.
    /// </summary>
    private async Task<TaskAccess?> LoadAsync(
        TaskActor actor,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await store.FindTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        var projectAccess = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (projectAccess is null)
        {
            return null;
        }

        var canManage = actor.UserId == task.OwnerId || projectAccess.IsOwner;

        if (projectAccess.IsInsider)
        {
            return new TaskAccess(task, projectAccess, SummaryOnly: false, CanManage: canManage);
        }

        // A visitor only reads the tasks of a public and active project.
        if (!string.Equals(projectAccess.Visibility, "public", StringComparison.Ordinal) ||
            !projectAccess.IsActive)
        {
            return null;
        }

        return task.Visibility switch
        {
            TaskVisibility.Public =>
                new TaskAccess(task, projectAccess, SummaryOnly: false, CanManage: false),
            TaskVisibility.Matched =>
                new TaskAccess(task, projectAccess, SummaryOnly: true, CanManage: false),
            _ => null,
        };
    }

    private async Task<TaskDetailView> ToDetailAsync(TaskAccess access, CancellationToken cancellationToken)
    {
        var task = access.Task;
        var taskIds = new[] { task.Id };

        var requirements = (await store.ListRequirementsAsync(taskIds, cancellationToken))
            .GetValueOrDefault(task.Id, []);
        var steps = (await store.ListStepsAsync(taskIds, cancellationToken))
            .GetValueOrDefault(task.Id, []);
        var slots = (await store.ListSlotsAsync(taskIds, cancellationToken))
            .GetValueOrDefault(task.Id, []);
        var history = (await store.ListStatusHistoryAsync(taskIds, cancellationToken))
            .GetValueOrDefault(task.Id, []);

        var usernames = await store.ListUsernamesAsync([task.OwnerId], cancellationToken);
        var projectNames = await store.ListProjectNamesAsync([task.ProjectId], cancellationToken);

        var summaryOnly = access.SummaryOnly;

        return new TaskDetailView(
            task.Id,
            task.Title,
            summaryOnly ? string.Empty : task.Scope,
            ModeNames(task.Modes),
            VisibilityText(task.Visibility),
            StatusText(task.Status),
            task.ExpectedMinutes,
            task.BaseReward,
            task.SlotBonus,
            task.SlotCount,
            TaskRules.MaximumBudget(task.BaseReward, task.SlotBonus, task.SlotCount),
            task.ReservedBudget,
            task.Deadline,
            task.AcceptsCleanFindings,
            summaryOnly ? null : AccessKindText(task.AccessKind),
            summaryOnly ? null : task.AccessResource,
            summaryOnly ? null : task.ReviewCriteria,
            task.ProjectId,
            projectNames.GetValueOrDefault(task.ProjectId, string.Empty),
            usernames.GetValueOrDefault(task.OwnerId, string.Empty),
            task.Version,
            summaryOnly,
            access.CanManage,
            requirements
                .Select(requirement => new TaskRequirementView(requirement.Kind, requirement.Value))
                .ToArray(),
            summaryOnly
                ? []
                : steps
                    .OrderBy(step => step.Position)
                    .Select(step => new TaskStepView(step.Position, step.Title, step.ExpectedResult))
                    .ToArray(),
            slots
                .OrderBy(slot => slot.Position)
                .Select(slot => new TaskSlotView(slot.Id, slot.Position, slot.Reward, SlotStateText(slot.State), slot.StartedAt))
                .ToArray(),
            history
                .OrderBy(change => change.ChangedAt)
                .Select(change => new TaskStatusChangeView(
                    change.FromStatus,
                    change.ToStatus,
                    usernames.GetValueOrDefault(change.ChangedBy, string.Empty),
                    change.ChangedAt))
                .ToArray(),
            task.CreatedAt,
            task.UpdatedAt,
            task.PublishedAt,
            task.CancelledAt);
    }

    private static IReadOnlyList<TaskRequirement> BuildRequirements(Guid taskId, TaskInput input) =>
    [
        .. input.Requirements.Select(requirement => new TaskRequirement
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Kind = requirement.Kind.Trim().ToLowerInvariant(),
            Value = requirement.Value.Trim(),
        }),
    ];

    private static IReadOnlyList<TaskStep> BuildSteps(Guid taskId, TaskInput input) =>
    [
        .. input.Steps.Select((step, index) => new TaskStep
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Position = index + 1,
            Title = step.Title.Trim(),
            ExpectedResult = step.ExpectedResult.Trim(),
        }),
    ];

    private static TaskInput ToInput(
        MarketplaceTask task,
        IReadOnlyList<TaskStep>? steps = null,
        IReadOnlyList<TaskRequirement>? requirements = null) => new()
        {
            ProjectId = task.ProjectId,
            Title = task.Title,
            Scope = task.Scope,
            Modes = task.Modes,
            Visibility = task.Visibility,
            ExpectedMinutes = task.ExpectedMinutes,
            BaseReward = task.BaseReward,
            SlotBonus = task.SlotBonus,
            SlotCount = task.SlotCount,
            Deadline = task.Deadline,
            AcceptsCleanFindings = task.AcceptsCleanFindings,
            AccessKind = task.AccessKind,
            AccessResource = task.AccessResource,
            ReviewCriteria = task.ReviewCriteria,
            Steps = steps?
            .Select(step => new TaskStepInput(step.Title, step.ExpectedResult))
            .ToArray() ?? [],
            Requirements = requirements?
            .Select(requirement => new TaskRequirementInput(requirement.Kind, requirement.Value))
            .ToArray() ?? [],
        };

    private static ProjectActor ToProjectActor(TaskActor actor) =>
        new(actor.UserId, actor.Username);

    private static TaskCommandResult NotFound() =>
        new(TaskCommandStatus.NotFound, Message: "The task does not exist.");

    private static TaskCommandResult Conflict(int? currentVersion = null)
    {
        var message = "The task was changed somewhere else. The current version is " +
            (currentVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown") + ".";

        return new TaskCommandResult(
            TaskCommandStatus.VersionConflict,
            CurrentVersion: currentVersion,
            Message: message);
    }

    /// <summary>The reference of the escrow of a task in the ledger of the credits.</summary>
    public static string EscrowReference(Guid taskId) => $"task:{taskId:D}";

    /// <summary>The wire names of the modes of a task.</summary>
    public static IReadOnlyList<string> ModeNames(TaskMode modes)
    {
        var names = new List<string>(4);

        if (modes.HasFlag(TaskMode.Explore))
        {
            names.Add("explore");
        }

        if (modes.HasFlag(TaskMode.Verify))
        {
            names.Add("verify");
        }

        if (modes.HasFlag(TaskMode.Access))
        {
            names.Add("access");
        }

        if (modes.HasFlag(TaskMode.Review))
        {
            names.Add("review");
        }

        return names;
    }

    /// <summary>The wire name of a lifecycle state of a task.</summary>
    public static string StatusText(TaskState status) => status switch
    {
        TaskState.Open => "open",
        TaskState.InProgress => "in_progress",
        TaskState.Cancelled => "cancelled",
        _ => "draft",
    };

    /// <summary>The wire name of the visibility of a task.</summary>
    public static string VisibilityText(TaskVisibility visibility) => visibility switch
    {
        TaskVisibility.Public => "public",
        TaskVisibility.Matched => "matched",
        _ => "private",
    };

    /// <summary>The wire name of the kind of the access of a task.</summary>
    public static string? AccessKindText(TaskAccessKind? kind) => kind switch
    {
        TaskAccessKind.OwnerOperated => "owner_operated",
        TaskAccessKind.DelegatedConsent => "delegated_consent",
        _ => null,
    };

    /// <summary>The wire name of the state of a slot of a task.</summary>
    public static string SlotStateText(TaskSlotState state) => state switch
    {
        TaskSlotState.Leased => "leased",
        TaskSlotState.Active => "active",
        TaskSlotState.Submitted => "submitted",
        TaskSlotState.Disputed => "disputed",
        TaskSlotState.Settled => "settled",
        TaskSlotState.Refunded => "refunded",
        _ => "available",
    };

    /// <summary>The task and what the actor may do with it.</summary>
    private sealed record TaskAccess(
        MarketplaceTask Task,
        ProjectAccessView Project,
        bool SummaryOnly,
        bool CanManage);
}
