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
    /// The task is readable by the accounts that satisfy every hard condition
    /// of the task, and by the owner and the members of the project. The
    /// instructions of the task stay with the insiders of the project.
    /// </summary>
    Matched,

    /// <summary>Only the owner and the members of the parent project may read the task.</summary>
    Private,
}
