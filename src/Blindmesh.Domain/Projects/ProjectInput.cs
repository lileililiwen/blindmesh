namespace Blindmesh.Domain.Projects;

/// <summary>
/// The editable content of a project. The service merges a change into the
/// current content and validates the result, so there is one definition of a
/// valid project.
/// </summary>
public sealed class ProjectInput
{
    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.Private;
}
