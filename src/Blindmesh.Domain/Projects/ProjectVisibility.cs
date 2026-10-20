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
    /// Only the owner, the members and the eligible candidates of the matching
    /// scope may read it. The matching scope is owned by a later package, so
    /// only the owner and the members can read a matched project today.
    /// </summary>
    Matched,
}
