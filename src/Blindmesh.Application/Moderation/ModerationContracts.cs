using Blindmesh.Domain.Identity;

namespace Blindmesh.Application.Moderation;

/// <summary>
/// The actor of a moderation command. The role decides whether the account may
/// act at all; the conflict rule of the module decides whether the account may
/// act on this case.
/// </summary>
public sealed record ModerationActor(Guid? UserId, string Username, string Role)
{
    public static ModerationActor Anonymous { get; } = new(null, string.Empty, UserRole.User);

    public bool IsAuthenticated => UserId.HasValue;

    /// <summary>True when the account is moderation staff.</summary>
    public bool IsStaff => Role is UserRole.Moderator or UserRole.Administrator;

    /// <summary>True when the account is an administrator.</summary>
    public bool IsAdministrator => Role == UserRole.Administrator;
}

/// <summary>The result of one command of the moderation module.</summary>
public enum ModerationCommandStatus
{
    /// <summary>The command changed the case.</summary>
    Succeeded,

    /// <summary>The command created a row.</summary>
    Created,

    /// <summary>The resource does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The resource is visible but the actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>The expected version of the case is not the current one.</summary>
    VersionConflict,

    /// <summary>The requested transition is not allowed by the state machine.</summary>
    InvalidTransition,

    /// <summary>The state of the case refused the command.</summary>
    StateConflict,

    /// <summary>The escrow of the case could not be moved.</summary>
    Conflict,

    /// <summary>The award of the case cannot be reversed any more.</summary>
    NotReversible,

    /// <summary>The service could not answer.</summary>
    Unavailable,
}

/// <summary>The result of one command of the moderation module.</summary>
public sealed record ModerationCommandResult(
    ModerationCommandStatus Status,
    ReportReceiptView? Receipt = null,
    ReportView? Report = null,
    DisputeView? Dispute = null,
    BanView? Ban = null,
    IReadOnlyList<ReportView>? Reports = null,
    IReadOnlyList<DisputeSummaryView>? Disputes = null,
    IReadOnlyList<DisputeEvidenceView>? Evidence = null,
    IReadOnlyList<ModerationAuditView>? Audit = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Message = null,
    int? CurrentVersion = null);

/// <summary>
/// The receipt of a report. It is the only part of a report the reporter reads
/// back: it never carries the body of the report nor the identity of the
/// reporter.
/// </summary>
public sealed record ReportReceiptView(
    Guid Id,
    string Receipt,
    string State,
    DateTimeOffset CreatedAt);

/// <summary>
/// A report as the moderation staff reads it. The identity of the reporter is
/// part of the row for the staff and never leaves the moderation module.
/// </summary>
public sealed record ReportView(
    Guid Id,
    string Receipt,
    string State,
    string SubjectType,
    Guid? SubjectId,
    string Reason,
    string? Resolution,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

/// <summary>The summary of a case, as the list of the moderation shows it.</summary>
public sealed record DisputeSummaryView(
    Guid Id,
    string State,
    Guid SubmissionId,
    Guid TaskId,
    long BaseReward,
    bool? AwardToContributor,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>
/// The case of an arbitration. The parties read the decision and the ledger
/// result of it; the identity of the reporter of the underlying report is
/// never part of the answer.
/// </summary>
public sealed record DisputeView(
    Guid Id,
    string State,
    Guid SubmissionId,
    Guid ParticipationId,
    Guid TaskId,
    long BaseReward,
    bool? AwardToContributor,
    string? ResolutionReason,
    Guid? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    Guid? LedgerTransactionId,
    string? LedgerTransactionKind,
    long? LedgerAmount,
    Guid? AppealedBy,
    DateTimeOffset? AppealedAt,
    string? AppealReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    int Version,
    string? Reason,
    string? EvidenceNote,
    long? AwardedAmount);

/// <summary>One line of the trail of a case.</summary>
public sealed record ModerationAuditView(
    Guid Id,
    string Action,
    string Reason,
    string? Details,
    DateTimeOffset CreatedAt);

/// <summary>One piece of evidence of a case.</summary>
public sealed record DisputeEvidenceView(
    Guid Id,
    string Kind,
    string Note,
    DateTimeOffset CreatedAt);

/// <summary>The standing of a banned account, as the administrator reads it.</summary>
public sealed record BanView(
    Guid UserId,
    bool IsBanned,
    DateTimeOffset? BannedUntil,
    string? BannedReason,
    DateTimeOffset? BannedAt);

/// <summary>The command that opens a report.</summary>
public sealed record OpenReportInput(
    string Reason,
    string? SubjectType = null,
    Guid? SubjectId = null);

/// <summary>The command that opens the dispute of a seat.</summary>
public sealed record OpenDisputeInput(
    string Reason,
    int? ExpectedVersion = null);

/// <summary>The command that asks the parties of a case for more evidence.</summary>
public sealed record RequestEvidenceInput(
    string Note,
    int? ExpectedVersion = null);

/// <summary>The command that decides a case and moves the escrow.</summary>
public sealed record ResolveDisputeInput(
    bool AwardBase,
    string Reason,
    int? ExpectedVersion = null,
    long Bonus = 0);

/// <summary>The command that closes a case.</summary>
public sealed record CloseDisputeInput(
    string? Reason = null,
    int? ExpectedVersion = null);

/// <summary>The command that appeals a decision.</summary>
public sealed record AppealDisputeInput(
    string Reason,
    int? ExpectedVersion = null);

/// <summary>The command that bans an account.</summary>
public sealed record BanUserInput(
    string Reason,
    DateTimeOffset? Until = null);
