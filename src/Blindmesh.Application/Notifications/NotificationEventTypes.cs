namespace Blindmesh.Application.Notifications;

/// <summary>
/// The event types of the notification module. They name the workflow events
/// that carry a notification. The identity of a concrete event is the type
/// together with the aggregate of the event, so the same kind of event on two
/// aggregates never shares one delivery.
/// </summary>
public static class NotificationEventTypes
{
    /// <summary>The owner of a task received an application for a seat.</summary>
    public const string ApplicationReceived = "notification.application.received";

    /// <summary>The owner of a task accepted an application for a seat.</summary>
    public const string ApplicationAccepted = "notification.application.accepted";

    /// <summary>A deadline of the work of the recipient is near.</summary>
    public const string Deadline = "notification.deadline";

    /// <summary>The owner of a task received the evidence of a seat.</summary>
    public const string SubmissionReceived = "notification.submission.received";

    /// <summary>The owner of a task asked the contributor for a correction.</summary>
    public const string CorrectionRequested = "notification.correction.requested";

    /// <summary>The work of a seat was finally accepted.</summary>
    public const string Acceptance = "notification.acceptance";

    /// <summary>Credits were returned to the wallet of the recipient.</summary>
    public const string Refund = "notification.refund";

    /// <summary>A case of arbitration changed for one of its parties.</summary>
    public const string DisputeUpdate = "notification.dispute.update";

    /// <summary>A moderation decision changed the state of the work.</summary>
    public const string ModerationOutcome = "notification.moderation.outcome";

    /// <summary>The operator of the product is told that a delivery died.</summary>
    public const string OperatorAlert = "notification.operator.alert";

    /// <summary>
    /// The identity of one concrete event of the workflow. The aggregate of
    /// the event is part of it, so two events of the same kind on two
    /// aggregates never share one delivery identity.
    /// </summary>
    public static string Identity(string eventType, Guid aggregateId) =>
        $"{eventType}.{aggregateId:D}";
}
