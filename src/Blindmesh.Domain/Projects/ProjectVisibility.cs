namespace Blindmesh.Domain.Projects;

/// <summary>
/// Who may read a project. The visibility is enforced on every read, list,
/// detail and membership operation.
/// </summary>
public enum ProjectVisibility
{
    /// <summary>Every visitor may read the project, also without a session.</summary>
    Public,

    /// <summary>Only the owner and the members of the project may read it.</summary>
    Private,

    /// <summary>
    /// Only the owner, the members and the accounts that fit one of the open
    /// tasks of the project may read it. The rule of the scope of the matching
    /// module decides, so the project and its tasks never disagree.
    /// </summary>
    Matched,
}
