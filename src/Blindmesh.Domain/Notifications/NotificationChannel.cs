namespace Blindmesh.Domain.Notifications;

/// <summary>
/// The channel a notification travels through. The in-app channel is durable
/// and readable inside the product; the email channel is a retryable copy
/// that never carries private content.
/// </summary>
public enum NotificationChannel
{
    /// <summary>The notification is stored and shown in the product.</summary>
    InApp,

    /// <summary>The notification is sent to the mailbox of the recipient.</summary>
    Email,
}
