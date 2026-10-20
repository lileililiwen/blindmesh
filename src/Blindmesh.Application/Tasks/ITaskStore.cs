using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The persistence of the tasks module. The store is scoped, so it shares the
/// scoped database context and the transaction of the request with the escrow
/// primitives of the credit module.
/// </summary>
public interface ITaskStore
{
    /// <summary>The task of the given id. Null when there is no such task.</summary>
    Task<MarketplaceTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// The task of the given id, locked for the transaction of the caller. The
    /// lock of the task comes before the lock of the accounts, as the shared
    /// contract of the lock order demands.
    /// </summary>
    Task<MarketplaceTask?> LockTaskAsync(Guid taskId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskRequirement>>> ListRequirementsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskStep>>> ListStepsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskSlot>>> ListSlotsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskStatusChange>>> ListStatusHistoryAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken);

    /// <summary>The tasks the actor may see, in the stable descending order.</summary>
    Task<TaskPage> ListVisibleAsync(Guid? actorId, TaskListQuery query, CancellationToken cancellationToken);

    /// <summary>The usernames of the given contributors.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ListUsernamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>The names of the given projects.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ListProjectNamesAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken);

    Task AddTaskAsync(MarketplaceTask task, CancellationToken cancellationToken);

    Task AddRequirementsAsync(IReadOnlyList<TaskRequirement> requirements, CancellationToken cancellationToken);

    Task AddStepsAsync(IReadOnlyList<TaskStep> steps, CancellationToken cancellationToken);

    Task AddSlotsAsync(IReadOnlyList<TaskSlot> slots, CancellationToken cancellationToken);

    Task AddStatusChangeAsync(TaskStatusChange change, CancellationToken cancellationToken);

    /// <summary>Replaces the requirements of a task with the given ones.</summary>
    Task ReplaceRequirementsAsync(
        Guid taskId,
        IReadOnlyList<TaskRequirement> requirements,
        CancellationToken cancellationToken);

    /// <summary>Replaces the steps of a task with the given ones.</summary>
    Task ReplaceStepsAsync(Guid taskId, IReadOnlyList<TaskStep> steps, CancellationToken cancellationToken);

    /// <summary>Writes the changes of the caller. False when another request changed the task first.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
