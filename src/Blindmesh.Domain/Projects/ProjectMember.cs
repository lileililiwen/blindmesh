namespace Blindmesh.Domain.Projects;

/// <summary>
/// A member of a project. The owner of the project is not stored here: the
/// owner is the owner of the project row and is the only member manager.
/// </summary>
public sealed class ProjectMember
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public ProjectMemberRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
