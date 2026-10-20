namespace Blindmesh.Domain.Projects;

/// <summary>
/// One applied transition of the lifecycle of a project. Every transition is
/// recorded, so the history of a project stays readable after the archive.
/// </summary>
public sealed class ProjectStatusChange
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public ProjectStatus FromStatus { get; set; }

    public ProjectStatus ToStatus { get; set; }

    /// <summary>The contributor that applied the transition.</summary>
    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
