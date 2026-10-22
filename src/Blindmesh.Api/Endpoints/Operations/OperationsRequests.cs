namespace Blindmesh.Api.Endpoints.Operations;

/// <summary>The body of the command that records a backup.</summary>
public sealed record RecordBackupRequest(
    string? Kind,
    string? Location,
    long? SizeBytes,
    string? ChecksumSha256,
    DateTimeOffset? RetentionUntil,
    string? Detail);

/// <summary>
/// The body of the command that runs a restore rehearsal. The two numbers are
/// the counts of the browser journeys and of the runs of axe of the current
/// pilot run; the operator enters them.
/// </summary>
public sealed record RunRestoreRehearsalRequest(
    Guid? BackupId,
    int? BrowserJourneys,
    int? AxeRuns);

/// <summary>The body of the command that handles a deletion request.</summary>
public sealed record CreateDeletionRequestRequest(
    Guid? SubjectUserId,
    string? Scope);
