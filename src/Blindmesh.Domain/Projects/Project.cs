namespace Blindmesh.Domain.Projects;

/// <summary>
/// A project of the marketplace. The row holds the editable content, the
/// visibility, the lifecycle state and the optimistic concurrency version. A
/// project is never deleted; it is archived and keeps its activity.
/// </summary>
public sealed class Project
{
    public Guid Id { get; set; }

    /// <summary>The owner of the project. The owner is the only member manager.</summary>
    public Guid OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The address of the repository of the project. It is an HTTPS address.</summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.Private;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>The optimistic concurrency version of the editable project.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
