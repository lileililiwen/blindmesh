namespace Blindmesh.Domain.Notifications;

/// <summary>
/// One delivery of one notification to one recipient through one channel. The
/// row is the queue of the notification module: it holds its own state, its own
/// attempts and its own schedule, so a failing mail never blocks the other
/// deliveries.
/// </summary>
public sealed class NotificationDelivery
{
    public Guid Id { get; set; }

    /// <summary>The recipient of the notification.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>
    /// The identity of the workflow event that produced the notification. It
    /// carries the aggregate of the event, so the same kind of event on two
    /// aggregates never shares one identity.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    public NotificationCategory Category { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>
    /// The identity of the delivery: the event, the recipient and the channel.
    /// It is unique, so a redelivered event never writes a second row for the
    /// same identity.
    /// </summary>
    public string UniqueKey { get; set; } = string.Empty;

    /// <summary>The short, safe subject of the notification.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The safe generic summary of the notification. It never holds a title, a
    /// finding text, a party, an amount or a link.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// The relative path inside the product that opens the source of the
    /// notification. It is never an absolute address and it never holds a
    /// token.
    /// </summary>
    public string DeepLink { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>The number of attempts the worker already made.</summary>
    public int AttemptCount { get; set; }

    /// <summary>The moment the delivery may be claimed again.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>
    /// The stable message identifier of the delivery. It is derived from the
    /// id of the delivery, so a retry of the same delivery keeps the same
    /// identifier and a receiver can recognise a duplicate.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The redacted reason of the last failed attempt. It never holds a
    /// provider detail, a host, a port or a credential.
    /// </summary>
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }

    /// <summary>The wire name of the channel.</summary>
    public static string ChannelText(NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => "in_app",
        NotificationChannel.Email => "email",
        _ => "unknown",
    };

    /// <summary>The wire name of the status.</summary>
    public static string StatusText(NotificationStatus status) => status switch
    {
        NotificationStatus.Pending => "pending",
        NotificationStatus.Sending => "sending",
        NotificationStatus.Sent => "sent",
        NotificationStatus.Failed => "failed",
        NotificationStatus.DeadLettered => "dead_lettered",
        _ => "unknown",
    };

    /// <summary>The wire name of the category.</summary>
    public static string CategoryText(NotificationCategory category) => category switch
    {
        NotificationCategory.ApplicationReceived => "application_received",
        NotificationCategory.ApplicationAccepted => "application_accepted",
        NotificationCategory.Deadline => "deadline",
        NotificationCategory.SubmissionReceived => "submission_received",
        NotificationCategory.CorrectionRequested => "correction_requested",
        NotificationCategory.Acceptance => "acceptance",
        NotificationCategory.Refund => "refund",
        NotificationCategory.DisputeUpdate => "dispute_update",
        NotificationCategory.ModerationOutcome => "moderation_outcome",
        _ => "unknown",
    };
}
