namespace Blindmesh.Application.Findings;

/// <summary>
/// The outbox event types owned by the findings package. The package writes
/// them inside the transaction of its own command, so an event is never stored
/// without the state change that produced it. The consumers re-check the
/// access to the source of the event.
/// </summary>
public static class FindingsEventTypes
{
    /// <summary>A seat was finally accepted.</summary>
    public const string ParticipationAccepted = "findings.participation.accepted";

    /// <summary>A finding of a contributor reached the confirmed state.</summary>
    public const string FindingConfirmed = "findings.finding.confirmed";

    /// <summary>The owner of a task received the evidence of a seat.</summary>
    public const string SubmissionReceived = "findings.submission.received";

    /// <summary>The owner of a task asked the contributor for a correction.</summary>
    public const string CorrectionRequested = "findings.correction.requested";
}

/// <summary>
/// The payload of the outbox events of the findings package. It names the seat
/// and the parties of the outcome; it never holds the text of a finding, the
/// title of a project or a link to a file.
/// </summary>
public sealed record FindingsEventPayload(
    Guid ParticipationId,
    Guid SubmissionId,
    Guid TaskId,
    Guid ProjectId,
    Guid ContributorId,
    Guid PublisherId,
    Guid? FindingId,
    long Amount,
    string Category,
    DateTimeOffset OccurredAt);
