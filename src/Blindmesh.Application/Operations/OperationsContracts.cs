using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Operations;

namespace Blindmesh.Application.Operations;

/// <summary>
/// The actor of a command of the operations module. The role decides whether
/// the account may act at all. The role is read again on every request, so a
/// demoted account loses the access at once.
/// </summary>
public sealed record OperationsActor(Guid? UserId, string Username, string Role)
{
    public static OperationsActor Anonymous { get; } = new(null, string.Empty, UserRole.User);

    public bool IsAuthenticated => UserId.HasValue;

    /// <summary>True when the account administers the product.</summary>
    public bool IsAdministrator => Role == UserRole.Administrator;
}

/// <summary>One backup of the product as the operator reads it.</summary>
public sealed record BackupView(
    Guid Id,
    DateTimeOffset TakenAt,
    string Kind,
    string Location,
    long SizeBytes,
    string ChecksumSha256,
    string Status,
    DateTimeOffset RetentionUntil,
    string Detail);

/// <summary>One restore rehearsal as the operator reads it.</summary>
public sealed record RestoreRehearsalView(
    Guid Id,
    Guid? BackupId,
    string Namespace,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string SchemaCheck,
    string DataCheck,
    string ObjectCheck,
    string LedgerCheck,
    string Notes,
    int? BrowserJourneys,
    int? AxeRuns);

/// <summary>
/// One deletion request as the operator reads it. The answer carries the
/// pseudonymous reference of the subject and never the removed identity.
/// </summary>
public sealed record DeletionRequestView(
    Guid Id,
    DateTimeOffset ReceivedAt,
    string Scope,
    string Status,
    string PseudonymizedUserId,
    string AuditReference);

/// <summary>One item of the pilot gate.</summary>
public sealed record PilotGateItemView(
    string Name,
    string Measured,
    string Expected,
    bool Passed,
    string Command);

/// <summary>
/// The measured gate of the pilot. It holds the real measured counts and it
/// never holds a target as a measurement.
/// </summary>
public sealed record PilotGateView(
    IReadOnlyList<PilotGateItemView> Items,
    bool Ready,
    DateTimeOffset MeasuredAt);

/// <summary>One dependency of the public status of the product.</summary>
public sealed record PublicStatusDependency(string Name, string Status, string Detail);

/// <summary>The measured counts of the marketplace.</summary>
public sealed record PublicStatusMarketplace(
    long PublishedTasks,
    long AcceptedContributions,
    long OpenCases,
    long Contributors);

/// <summary>
/// The public status of the product. It holds the state of the runtime and of
/// its dependencies, the measured counts of the marketplace and the honest
/// statement of what is not implemented yet. It never holds a private field, an
/// identity of a person or the amount of the ledger of a person.
/// </summary>
public sealed record PublicStatusView(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<PublicStatusDependency> Dependencies,
    PublicStatusMarketplace Marketplace,
    IReadOnlyList<string> NotImplemented);

/// <summary>The backups of the product.</summary>
public sealed record BackupListResponse(IReadOnlyList<BackupView> Items);

/// <summary>The restore rehearsals of the product.</summary>
public sealed record RestoreRehearsalListResponse(IReadOnlyList<RestoreRehearsalView> Items);

/// <summary>The deletion requests of the product.</summary>
public sealed record DeletionRequestListResponse(IReadOnlyList<DeletionRequestView> Items);

/// <summary>The backup the operator took.</summary>
public sealed record RecordBackupInput(
    string Kind,
    string Location,
    long SizeBytes,
    string ChecksumSha256,
    DateTimeOffset RetentionUntil,
    string Detail);

/// <summary>
/// The rehearsal the operator starts. The backup is empty when the rehearsal
/// dumps the live database itself. The two numbers are the counts of the
/// browser journeys and of the runs of axe of the current pilot run; the
/// operator enters them and they are reported by the gate as they are.
/// </summary>
public sealed record RunRestoreRehearsalInput(
    Guid? BackupId,
    int? BrowserJourneys,
    int? AxeRuns);

/// <summary>The deletion request of the operator.</summary>
public sealed record DeletionRequestInput(Guid SubjectUserId, string Scope);

/// <summary>The state of one command of the operations module.</summary>
public enum OperationsCommandStatus
{
    /// <summary>The command succeeded.</summary>
    Succeeded,

    /// <summary>The command wrote a new row.</summary>
    Created,

    /// <summary>The record does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The record is visible but the actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>The command could not run. The runtime is not in a state that
    /// allows it.</summary>
    Unavailable,
}

/// <summary>The result of one command of the operations module.</summary>
public sealed record OperationsCommandResult(
    OperationsCommandStatus Status,
    BackupView? Backup = null,
    RestoreRehearsalView? Rehearsal = null,
    DeletionRequestView? Deletion = null,
    IReadOnlyList<BackupView>? Backups = null,
    IReadOnlyList<RestoreRehearsalView>? Rehearsals = null,
    IReadOnlyList<DeletionRequestView>? Deletions = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Message = null);
