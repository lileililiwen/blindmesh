namespace Blindmesh.Domain.Projects;

/// <summary>
/// The lifecycle state of a project. The allowed transitions live in
/// <see cref="ProjectRules"/>; the history of the applied transitions lives in
/// the status history of the project.
/// </summary>
public enum ProjectStatus
{
    /// <summary>The project is prepared and is not published yet.</summary>
    Draft,

    /// <summary>The project is published and accepts new work.</summary>
    Active,

    /// <summary>The project is published but does not accept new work.</summary>
    Paused,

    /// <summary>The project keeps its activity and accepts no new work.</summary>
    Archived,
}
