namespace Blindmesh.Application.Tasks;

/// <summary>
/// The seam of the matching module for the modules that keep the visibility of
/// a task. The rule of the scope is applied by one method of the matching
/// module, and every read of a task asks it, so a task that the list of the
/// marketplace does not show is never readable through another route.
/// </summary>
public interface ITaskMatchSeam
{
    /// <summary>
    /// The ids of the open tasks of a project whose hard conditions the
    /// account satisfies. The list is empty when the account fits no task of
    /// the project, so a project of the matched scope stays closed for it.
    /// </summary>
    Task<IReadOnlyList<Guid>> MatchingTaskIdsAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the account may read the task. An insider of the project of
    /// the task reads everything of its project. Every other account reads a
    /// public task of a public project, and a task of the matched scope only
    /// when it satisfies every hard condition of the task.
    /// </summary>
    Task<bool> MatchesAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken);
}
