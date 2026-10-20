using Blindmesh.Domain.Projects;

namespace Blindmesh.Application.Projects;

/// <summary>
/// The query of the list of visible projects. The limit is already clamped by
/// the caller, so no query of the product is unbounded.
/// </summary>
public sealed record ProjectListQuery(int Limit, ProjectCursor? Cursor);

/// <summary>One page of the visible projects and the position of the next page.</summary>
public sealed record ProjectPage(IReadOnlyList<Project> Items, ProjectCursor? Next);

/// <summary>
/// The persistence of the projects module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly. The write methods stage the change and the caller commits the
/// staged changes with <see cref="SaveChangesAsync"/> inside its transaction.
/// </summary>
public interface IProjectStore
{
    /// <summary>
    /// Resolves the id of a user by its id or by its case folded username.
    /// Null means that there is no such user.
    /// </summary>
    Task<Guid?> ResolveUserIdAsync(string reference, CancellationToken cancellationToken);

    Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectMember?> FindMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectVersion>> ListVersionsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectStatusChange>> ListStatusHistoryAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The usernames of the given users. A missing user is left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ListUsernamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The projects the actor may see, in the stable descending order of the
    /// creation time and the id.
    /// </summary>
    Task<ProjectPage> ListVisibleAsync(Guid? actorId, ProjectListQuery query, CancellationToken cancellationToken);

    Task AddProjectAsync(Project project, CancellationToken cancellationToken);

    Task AddMemberAsync(ProjectMember member, CancellationToken cancellationToken);

    Task AddVersionAsync(ProjectVersion version, CancellationToken cancellationToken);

    Task AddStatusChangeAsync(ProjectStatusChange change, CancellationToken cancellationToken);

    /// <summary>Removes a member. False means that the project has no such member.</summary>
    Task<bool> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Commits the staged changes. False means that another request changed
    /// the same project first, so the caller has to report a version conflict.
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
