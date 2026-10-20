namespace Blindmesh.Api.Endpoints.Projects;

/// <summary>The body of a create command of a project.</summary>
public sealed record CreateProjectRequest(
    string? Name,
    string? Summary,
    string? Description,
    string? RepositoryUrl,
    string? Visibility);

/// <summary>
/// The body of a change of a project. A field that the caller left out keeps
/// its current value.
/// </summary>
public sealed record UpdateProjectRequest(
    int? ExpectedVersion,
    string? Name,
    string? Summary,
    string? Description,
    string? RepositoryUrl,
    string? Visibility);

/// <summary>The body of a transition of the lifecycle of a project.</summary>
public sealed record TransitionProjectRequest(int? ExpectedVersion, string? To);

/// <summary>The body of an add member command of a project.</summary>
public sealed record AddProjectMemberRequest(string? Role);

/// <summary>The body of an add version command of a project.</summary>
public sealed record AddProjectVersionRequest(string? Label, string? CommitReference);
