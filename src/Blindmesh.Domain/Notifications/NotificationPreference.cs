namespace Blindmesh.Domain.Notifications;

/// <summary>
/// The preferences of one recipient. The row is written when the recipient
/// changes a preference; a missing row means that both channels are on.
/// </summary>
public sealed class NotificationPreference
{
    /// <summary>The recipient the preferences belong to. It is the key of the row.</summary>
    public Guid UserId { get; set; }

    /// <summary>The recipient wants the in-app notifications.</summary>
    public bool InAppEnabled { get; set; } = true;

    /// <summary>The recipient wants the email notifications.</summary>
    public bool EmailEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}
