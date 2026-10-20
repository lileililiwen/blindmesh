namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The visibility of a task. The visibility of the parent project is the outer
/// limit: a task of a private project is private for everybody but the owner
/// and the members of the project.
/// </summary>
public enum TaskVisibility
{
    /// <summary>Everybody may read the whole task.</summary>
    Public,

    /// <summary>
    /// The summary of the task is readable, the instructions are not. The
    /// instructions are released by the matching of the marketplace.
    /// </summary>
    Matched,

    /// <summary>Only the owner and the members of the parent project may read the task.</summary>
    Private,
}
