using Blindmesh.Domain.Projects;

namespace Blindmesh.Application.Projects;

/// <summary>
/// The fields of a project a caller may change. A field that the caller left
/// out keeps its current value.
/// </summary>
public sealed class ProjectPatch
{
    public string? Name { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? RepositoryUrl { get; init; }

    /// <summary>The visibility is administered by the owner of the project only.</summary>
    public ProjectVisibility? Visibility { get; init; }
}
