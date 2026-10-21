namespace Blindmesh.Application.Reputation;

/// <summary>
/// One event of the outbox that the reputation module applies. The service
/// owns the category and the change of the score; the caller only names the
/// source of the event.
/// </summary>
public sealed record ReputationEventInput(
    /// <summary>The outbox message that produced the event.</summary>
    Guid SourceEventId,

    /// <summary>The contributor the event belongs to.</summary>
    Guid UserId,

    /// <summary>The seat the event comes from, when it is known.</summary>
    Guid? SourceParticipationId,

    /// <summary>The evidence of the seat the event comes from, when it is known.</summary>
    Guid? SourceSubmissionId,

    /// <summary>The finding the event comes from, when it is known.</summary>
    Guid? SourceFindingId,

    /// <summary>The project the event comes from, when it is known.</summary>
    Guid? SourceProjectId,

    /// <summary>The task the event comes from, when it is known.</summary>
    Guid? SourceTaskId,

    /// <summary>The moment the source of the event happened.</summary>
    DateTimeOffset OccurredAt);

/// <summary>The outcome of an applied reputation event.</summary>
public enum ReputationApplyStatus
{
    /// <summary>The event was written and the score moved.</summary>
    Succeeded,

    /// <summary>The same event was already stored, so nothing changed.</summary>
    Duplicate,
}

/// <summary>
/// The counts of a contributor after an event was applied. The numbers of a
/// duplicate event are the numbers that are already stored.
/// </summary>
public sealed record ReputationApplyResult(
    ReputationApplyStatus Status,
    int CurrentScore,
    int AcceptedContributions,
    int ConfirmedFindings,
    int ReversedContributions);

/// <summary>
/// The public summary of the reputation of a contributor. It holds counts and
/// never a source of a contribution.
/// </summary>
public sealed record ReputationSummaryView(
    string Username,
    int CurrentScore,
    int AcceptedContributions,
    int ConfirmedFindings,
    int ReversedContributions,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// One line of a history of reputation. The references of the source are only
/// filled when the reader may read them, so a public reader never receives
/// the title of a private project, a finding text, a party or a link.
/// </summary>
public sealed record ReputationEventView(
    Guid Id,
    string Category,
    string Label,
    int Delta,
    DateTimeOffset OccurredAt,
    string? ProjectTitle,
    Guid? TaskId,
    Guid? FindingId,
    Guid? ParticipationId);

/// <summary>
/// The history of the reputation of a contributor. The public history holds the
/// lines of the sources that are public right now; the own history holds every
/// line of the contributor with the references the contributor may read.
/// </summary>
public sealed record ReputationHistoryView(
    string Username,
    int CurrentScore,
    int AcceptedContributions,
    int ConfirmedFindings,
    int ReversedContributions,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ReputationEventView> Events);
