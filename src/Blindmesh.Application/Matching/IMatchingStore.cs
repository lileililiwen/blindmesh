using Blindmesh.Domain.Projects;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Matching;

/// <summary>
/// The persistence of the matching module. The module owns no table and adds
/// no migration: the store reads the rows of the task, of the project and of
/// the account, and it writes nothing.
/// </summary>
public interface IMatchingStore
{
    /// <summary>
    /// The task and the scope of its project, as the account sees it. Null
    /// means that there is no such task. The record tells whether the account
    /// is an insider of the project of the task.
    /// </summary>
    Task<MatchingTask?> FindTaskAsync(
        Guid taskId,
        Guid accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The tasks of a project that are in the marketplace, the open ones and
    /// the running ones.
    /// </summary>
    Task<IReadOnlyList<MatchingTask>> ListProjectTasksAsync(
        Guid projectId,
        Guid accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The public identity of the accounts of the ids. An account that is not
    /// known is left out.
    /// </summary>
    Task<IReadOnlyList<MatchingAccount>> ListAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// A task and the scope of its project, as the matching module reads it. The
/// record holds the visibility and the state of the task and of its project
/// and the place of the account in the project.
/// </summary>
public sealed record MatchingTask(
    Guid Id,
    Guid ProjectId,
    TaskVisibility Visibility,
    TaskState Status,
    ProjectVisibility ProjectVisibility,
    ProjectStatus ProjectStatus,
    bool IsInsider);

/// <summary>
/// The public identity of an account, as the matching module reads it. The
/// record never holds the email, the provider login or the biography of the
/// account.
/// </summary>
public sealed record MatchingAccount(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsPublicProfile,
    DateTimeOffset CreatedAt);
