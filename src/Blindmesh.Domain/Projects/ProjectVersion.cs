namespace Blindmesh.Domain.Projects;

/// <summary>
/// A released version of a project. The versions are the history of the
/// published state of the project and are never changed afterwards.
/// </summary>
public sealed class ProjectVersion
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>The label of the version, for example v1.2.0.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The optional reference of the commit or the tag of the version.</summary>
    public string? CommitReference { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
