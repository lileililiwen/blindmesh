namespace Blindmesh.Domain.Notifications;

/// <summary>
/// The kind of workflow event a notification belongs to. The category is the
/// only thing the recipient reads before the deep link is opened, so it never
/// carries a title, a finding text, a party or a link.
/// </summary>
public enum NotificationCategory
{
    /// <summary>The owner of a task received an application for a seat.</summary>
    ApplicationReceived,

    /// <summary>The owner of a task accepted an application for a seat.</summary>
    ApplicationAccepted,

    /// <summary>A deadline of the work of the recipient is near.</summary>
    Deadline,

    /// <summary>The owner of a task received the evidence of a seat.</summary>
    SubmissionReceived,

    /// <summary>The owner of a task asked the contributor for a correction.</summary>
    CorrectionRequested,

    /// <summary>The work of a seat was finally accepted.</summary>
    Acceptance,

    /// <summary>Credits were returned to the wallet of the recipient.</summary>
    Refund,

    /// <summary>A case of arbitration changed for one of its parties.</summary>
    DisputeUpdate,

    /// <summary>A moderation decision changed the state of the work.</summary>
    ModerationOutcome,
}
