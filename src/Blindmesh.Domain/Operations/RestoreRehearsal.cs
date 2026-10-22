namespace Blindmesh.Domain.Operations;

/// <summary>
/// One restore rehearsal. The rehearsal restored a backup into an isolated
/// database of its own, ran the four checks against that database and wrote
/// the verdicts here.
///
/// The four verdicts are short sentences. They name what was checked and what
/// the check found, and they never hold a provider name, a SQL statement, a
/// path of the host or a credential.
/// </summary>
public sealed class RestoreRehearsal
{
    public Guid Id { get; set; }

    /// <summary>The backup the rehearsal restored. It is empty when the
    /// rehearsal dumped the live database itself.</summary>
    public Guid? BackupId { get; set; }

    /// <summary>The name of the isolated database the rehearsal used.</summary>
    public string Namespace { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The state of the rehearsal.</summary>
    public RestoreRehearsalStatus Status { get; set; }

    /// <summary>The verdict of the check of the schema of the restored database.</summary>
    public string SchemaCheck { get; set; } = string.Empty;

    /// <summary>The verdict of the check of the rows of the restored database.</summary>
    public string DataCheck { get; set; } = string.Empty;

    /// <summary>The verdict of the check of the objects of the restored database.</summary>
    public string ObjectCheck { get; set; } = string.Empty;

    /// <summary>The verdict of the reconciliation of the ledger of the restored database.</summary>
    public string LedgerCheck { get; set; } = string.Empty;

    /// <summary>A redacted note of the operator about the rehearsal.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// The number of the browser journeys the operator recorded for the pilot
    /// run. It is empty when the operator entered no number yet.
    /// </summary>
    public int? BrowserJourneys { get; set; }

    /// <summary>
    /// The number of the runs of axe the operator recorded for the pilot run.
    /// It is empty when the operator entered no number yet.
    /// </summary>
    public int? AxeRuns { get; set; }
}
