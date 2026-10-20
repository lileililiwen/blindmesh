using Blindmesh.Domain.Findings;

namespace Blindmesh.Application.Findings;

/// <summary>
/// The actor of a command of the findings module. A visitor without a session
/// is an anonymous actor: it may read nothing that belongs to a seat.
/// </summary>
public sealed record FindingsActor(Guid? UserId, string Username)
{
    public static FindingsActor Anonymous { get; } = new(null, string.Empty);

    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>
/// The outcome of a command of the findings module. The status names the
/// reason of a refusal, so a client can react without reading the message.
/// </summary>
public enum FindingsCommandStatus
{
    Succeeded,
    Created,
    NotFound,
    Forbidden,
    ValidationFailed,
    VersionConflict,
    InvalidTransition,
    StateConflict,
    PayloadTooLarge,
    Unavailable,
}

/// <summary>The result of one command of the findings module.</summary>
public sealed record FindingsCommandResult(
    FindingsCommandStatus Status,
    SubmissionDetailView? Submission = null,
    FindingView? Finding = null,
    AttachmentView? Attachment = null,
    AttachmentDownloadView? Download = null,
    IReadOnlyList<SubmissionSummaryView>? Reviews = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Message = null,
    int? CurrentVersion = null);

/// <summary>The result of one step of a verification.</summary>
public sealed record SubmissionStepResultView(int Position, string Title, string Result);

/// <summary>One private file of the evidence.</summary>
public sealed record AttachmentView(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string State,
    DateTimeOffset CreatedAt);

/// <summary>The signed address of a private file.</summary>
public sealed record AttachmentDownloadView(
    string Url,
    DateTimeOffset ExpiresAt,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>One applied transition of a finding.</summary>
public sealed record FindingHistoryView(
    string FromState,
    string ToState,
    string ChangedByUsername,
    DateTimeOffset ChangedAt);

/// <summary>One finding of a revision of a submission.</summary>
public sealed record FindingView(
    Guid Id,
    Guid SubmissionId,
    string Category,
    string Severity,
    string Title,
    string Reproduction,
    string ExpectedResult,
    string ActualResult,
    string State,
    string? DecisionNote,
    string DecidedByUsername,
    DateTimeOffset? DecidedAt,
    DateTimeOffset CreatedAt,
    int Version,
    IReadOnlyList<FindingHistoryView> History);

/// <summary>One revision of a submission.</summary>
public sealed record SubmissionRevisionView(
    Guid Id,
    int Number,
    string Summary,
    bool ScopeCompleted,
    IReadOnlyList<SubmissionStepResultView> Steps,
    IReadOnlyList<string> TestedAreas,
    string CleanResultExplanation,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AttachmentView> Attachments);

/// <summary>One applied transition of a submission.</summary>
public sealed record SubmissionStatusChangeView(
    string FromState,
    string ToState,
    string ChangedByUsername,
    DateTimeOffset ChangedAt);

/// <summary>One row of the list of the evidence that waits for a review.</summary>
public sealed record SubmissionReviewRow(
    Guid SubmissionId,
    Guid TaskId,
    string TaskTitle,
    Guid TaskOwnerId,
    Guid ContributorId,
    SubmissionState State,
    string Summary,
    bool ScopeCompleted,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CorrectionDeadline,
    long BaseReward,
    long SlotBonus,
    long BonusGranted,
    int Version);

/// <summary>One submission of the list of the reviews of a project.</summary>
public sealed record SubmissionSummaryView(
    Guid Id,
    Guid TaskId,
    string TaskTitle,
    string ContributorUsername,
    string State,
    string Summary,
    bool ScopeCompleted,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CorrectionDeadline,
    long BaseReward,
    long SlotBonus,
    long BonusGranted,
    int Version);

/// <summary>The committed state of one submission of one seat.</summary>
public sealed record SubmissionDetailView(
    Guid Id,
    Guid ParticipationId,
    Guid TaskId,
    string TaskTitle,
    Guid ProjectId,
    string ProjectName,
    string OwnerUsername,
    string ContributorUsername,
    string State,
    IReadOnlyList<string> Modes,
    string Summary,
    bool ScopeCompleted,
    IReadOnlyList<SubmissionRevisionView> Revisions,
    IReadOnlyList<FindingView> Findings,
    IReadOnlyList<SubmissionStatusChangeView> History,
    long BaseReward,
    long SlotBonus,
    long BonusGranted,
    long SettledCredits,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? CorrectionDeadline,
    DateTimeOffset? ReviewDeadline,
    string? DisputeReason,
    bool CanSubmit,
    bool CanReview,
    bool CanRequestChanges,
    bool CanDispute,
    bool CanUpload,
    int Version);

/// <summary>The result of one step of a verification, as the contributor sent it.</summary>
public sealed record SubmissionStepInput(int Position, string Title, string Result);

/// <summary>One finding, as the contributor sent it.</summary>
public sealed record FindingInput(
    string Category,
    string Severity,
    string Title,
    string Reproduction,
    string ExpectedResult,
    string ActualResult);

/// <summary>The evidence of a seat, as the contributor sent it.</summary>
public sealed record SubmissionInput(
    string Summary,
    bool ScopeCompleted,
    IReadOnlyList<SubmissionStepInput> Steps,
    IReadOnlyList<string> TestedAreas,
    string CleanResultExplanation,
    IReadOnlyList<FindingInput> Findings);

/// <summary>The command that accepts the work of a seat.</summary>
public sealed record AcceptSubmissionInput(long Bonus, int? ExpectedVersion);

/// <summary>The command that asks for the one correction of a seat.</summary>
public sealed record RequestChangesInput(string Note, int? ExpectedVersion);

/// <summary>The command that opens a dispute about the completion of a seat.</summary>
public sealed record DisputeSubmissionInput(string Reason, int? ExpectedVersion);

/// <summary>The command that classifies a finding.</summary>
public sealed record PatchFindingInput(string State, string? Note, int? ExpectedVersion);

/// <summary>The declaration of a private file of the evidence.</summary>
public sealed record AttachmentIntentInput(
    Guid SubmissionId,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>The report of the sweep of the deadlines of the review.</summary>
public sealed record ReviewSweepReport(int Accepted, int Disputed);
