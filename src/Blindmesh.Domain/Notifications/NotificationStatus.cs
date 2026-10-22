namespace Blindmesh.Domain.Notifications;

/// <summary>
/// The state of one delivery. The state machine of a delivery is its own: it
/// is not the state of the outbox message that produced it.
/// </summary>
public enum NotificationStatus
{
    /// <summary>The delivery waits for the worker that sends it.</summary>
    Pending,

    /// <summary>The worker holds the delivery and is sending it right now.</summary>
    Sending,

    /// <summary>The delivery reached the channel.</summary>
    Sent,

    /// <summary>The last attempt failed. The delivery waits for its next
    /// attempt or it is dead lettered.</summary>
    Failed,

    /// <summary>The delivery used every attempt. It never goes out again and an
    /// operator alert names it.</summary>
    DeadLettered,
}
