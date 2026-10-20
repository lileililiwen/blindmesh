namespace Blindmesh.Application.Projects;

/// <summary>
/// The actor of a project command. A visitor without a session is an
/// anonymous actor: it may read a public project and nothing else.
/// </summary>
public sealed record ProjectActor(Guid? UserId, string Username)
{
    public static ProjectActor Anonymous { get; } = new(null, string.Empty);

    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>
/// The access of an actor to a project. It is the answer of the project module
/// to another module of the product that keeps the visibility of a child
/// object, such as a task. It repeats no rule of the project module: it only
/// says what the actor may do with the project.
/// </summary>
public sealed record ProjectAccessView(
    bool IsOwner,
    bool IsInsider,
    bool CanEdit,
    bool IsActive,
    string Visibility,
    string Status);

/// <summary>One project of the list of visible projects.</summary>
public sealed record ProjectSummaryView(
    Guid Id,
    string Name,
    string Summary,
    string Status,
    string Visibility,
    string OwnerUsername,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>One member of a project.</summary>
public sealed record ProjectMemberView(
    Guid UserId,
    string Username,
    string Role,
    DateTimeOffset CreatedAt);

/// <summary>One released version of a project.</summary>
public sealed record ProjectVersionView(
    Guid Id,
    string Label,
    string? CommitReference,
    string CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>One applied transition of the lifecycle of a project.</summary>
public sealed record ProjectStatusChangeView(
    string FromStatus,
    string ToStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt);

/// <summary>
/// The committed state of a project with its membership, its versions and the
/// history of its lifecycle. The membership is only part of the answer for the
/// owner and the members; everybody else sees an empty membership.
/// </summary>
public sealed record ProjectDetailView(
    Guid Id,
    string Name,
    string Summary,
    string Description,
    string RepositoryUrl,
    string Status,
    string Visibility,
    int Version,
    bool AcceptsTasks,
    string OwnerUsername,
    IReadOnlyList<ProjectMemberView> Members,
    IReadOnlyList<ProjectVersionView> Versions,
    IReadOnlyList<ProjectStatusChangeView> StatusHistory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The outcome of a project command.</summary>
public enum ProjectCommandStatus
{
    /// <summary>The command changed the project.</summary>
    Succeeded,

    /// <summary>The command created the project.</summary>
    Created,

    /// <summary>The project does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The project is visible but the actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>The expected version of the project is not the current one.</summary>
    VersionConflict,

    /// <summary>The requested transition is not allowed by the state machine.</summary>
    InvalidTransition,

    /// <summary>The state of the project does not accept the command.</summary>
    StateConflict,
}

/// <summary>
/// The answer of a project command. The current version is part of the answer
/// of a version conflict, so the caller can reload the project.
/// </summary>
public sealed record ProjectCommandResult(
    ProjectCommandStatus Status,
    ProjectDetailView? Project = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    int? CurrentVersion = null,
    string? Message = null);
