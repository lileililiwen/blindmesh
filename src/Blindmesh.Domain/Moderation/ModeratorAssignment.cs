namespace Blindmesh.Domain.Moderation;

/// <summary>
/// The assignment of a case to a moderator. The row records who received the
/// case and who released it, so a moderator that already acted on a case can
/// never receive its appeal.
/// </summary>
public sealed class ModeratorAssignment
{
    public Guid Id { get; set; }

    /// <summary>The case the moderator received.</summary>
    public Guid DisputeId { get; set; }

    /// <summary>The moderator that received the case.</summary>
    public Guid ModeratorId { get; set; }

    /// <summary>The kind of the assignment: the first case or the appeal.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The moderator that made the assignment.</summary>
    public Guid? AssignedBy { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    /// <summary>When the assignment was released.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }
}
