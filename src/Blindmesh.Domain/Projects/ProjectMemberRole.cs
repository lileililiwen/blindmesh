namespace Blindmesh.Domain.Projects;

/// <summary>
/// The role of a member of a project. The owner is not a member role: the owner
/// is the owner of the project and is the only member manager.
/// </summary>
public enum ProjectMemberRole
{
    /// <summary>The member may read the project and apply for its work.</summary>
    Viewer,

    /// <summary>The member may read the project and change its content.</summary>
    Editor,
}
