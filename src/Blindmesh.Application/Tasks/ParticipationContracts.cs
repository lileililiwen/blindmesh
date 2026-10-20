using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The actor of a participation command. It carries the identity of the
/// contributor and nothing else, so a command never reads a credential.
/// </summary>
public sealed record ParticipationActor(Guid? UserId, string Username)
{
    public static ParticipationActor Anonymous { get; } = new(null, string.Empty);

    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>One applied transition of the lifecycle of a participation.</summary>
public sealed record ParticipationStatusChangeView(
    string FromStatus,
    string ToStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt);

/// <summary>
/// The summary of a participation. It never carries the scope or the steps of
/// the task, so the list of the work never leaks the instructions of a seat.
/// </summary>
public sealed record ParticipationSummaryView(
    Guid Id,
    Guid TaskId,
    string TaskTitle,
    Guid ProjectId,
    string ProjectName,
    string OwnerUsername,
    string ContributorUsername,
    string State,
    DateTimeOffset AppliedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StartDeadline,
    DateTimeOffset? ExecutionDeadline,
    long SlotReward,
    string SlotState,
    int Version);

/// <summary>
/// The committed state of a participation. The scope and the steps are only
/// filled when the reader holds the seat, so the instructions of a task are
/// released with the acceptance and never before.
/// </summary>
public sealed record ParticipationDetailView(
    Guid Id,
    Guid TaskId,
    string TaskTitle,
    Guid ProjectId,
    string ProjectName,
    string OwnerUsername,
    string ContributorUsername,
    string State,
    DateTimeOffset AppliedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StartDeadline,
    DateTimeOffset? ExecutionDeadline,
    DateTimeOffset? EndedAt,
    long SlotReward,
    string SlotState,
    bool ScopeConsent,
    bool InstructionsReleased,
    string? Scope,
    IReadOnlyList<TaskStepView> Steps,
    IReadOnlyList<string> Modes,
    IReadOnlyList<ParticipationStatusChangeView> History,
    bool CanAccept,
    bool CanDecline,
    bool CanStart,
    bool CanWithdraw,
    int Version);

/// <summary>The outcome of a participation command.</summary>
public enum ParticipationCommandStatus
{
    /// <summary>The command changed the participation.</summary>
    Succeeded,

    /// <summary>The command created the participation.</summary>
    Created,

    /// <summary>The participation or the task does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The participation is visible but the actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>The expected version of the task is not the current one.</summary>
    VersionConflict,

    /// <summary>The requested transition is not allowed by the state machine.</summary>
    InvalidTransition,

    /// <summary>The state of the task or of the participation does not accept the command.</summary>
    StateConflict,
}

/// <summary>The answer of a participation command.</summary>
public sealed record ParticipationCommandResult(
    ParticipationCommandStatus Status,
    ParticipationDetailView? Participation = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    int? CurrentVersion = null,
    string? Message = null);

/// <summary>The answer of a command that reads a list of seats.</summary>
public sealed record ParticipationListResult(
    ParticipationCommandStatus Status,
    ParticipationPage? Page = null,
    string? Message = null);

/// <summary>The input of the command that applies for a seat.</summary>
public sealed record ParticipationApplyInput(
    bool ScopeConsent,
    int? ExpectedVersion);

/// <summary>The query of the list of the participations.</summary>
public sealed record ParticipationListQuery(int Limit, ParticipationCursor? Cursor, Guid? TaskId);

/// <summary>One page of the list of the participations.</summary>
public sealed record ParticipationPage(
    IReadOnlyList<ParticipationSummaryView> Participations,
    ParticipationCursor? Next);

/// <summary>
/// The outcome of the sweep of the deadlines. It names the seats that the
/// sweep changed, so the worker and the tests can read what happened.
/// </summary>
public sealed record ParticipationExpiryReport(int Expired, int Released);
