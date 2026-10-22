using System.Security.Cryptography;
using System.Text;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Identity;
using Blindmesh.Domain.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blindmesh.Application.Operations;

/// <summary>
/// The service of the operations module. It records the backups of the product,
/// runs the restore rehearsals, handles the deletion requests of the
/// administrators and reports the measured gate of the pilot.
///
/// The service never writes a ledger row: the credits module stays the only
/// owner of the ledger. It only reads the ledger to reconcile it.
/// </summary>
public sealed class OperationsService(
    IOperationsStore store,
    IOperationsMeasurement measurement,
    IRestoreRehearsalEnvironment environment,
    IIdentityStore identities,
    HealthCheckService healthChecks,
    IClock clock)
{
    /// <summary>The tag of the health checks that the readiness of the product
    /// uses. The infrastructure registers the checks with the same tag.</summary>
    public const string ReadyTag = "ready";

    /// <summary>The number of days a backup is kept.</summary>
    public const int RetentionDays = 30;

    /// <summary>The longest note of an operator.</summary>
    public const int DetailMaxLength = 500;

    /// <summary>The longest scope of a deletion request.</summary>
    public const int ScopeMaxLength = 200;

    /// <summary>
    /// Records a backup the operator took. The checksum and the retention day
    /// are the facts the restore rehearsal needs.
    /// </summary>
    public async Task<OperationsCommandResult> RecordBackupAsync(
        OperationsActor actor,
        RecordBackupInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden();
        }

        var errors = ValidateBackup(input);

        if (errors.Count > 0)
        {
            return new OperationsCommandResult(
                OperationsCommandStatus.ValidationFailed,
                FieldErrors: errors,
                Message: "The backup could not be recorded because some fields are not valid.");
        }

        var backup = new BackupRecord
        {
            Id = Guid.NewGuid(),
            TakenAt = clock.UtcNow,
            Kind = string.Equals(input.Kind, "objects", StringComparison.OrdinalIgnoreCase)
                ? BackupKind.Objects
                : BackupKind.Database,
            Location = input.Location.Trim(),
            SizeBytes = input.SizeBytes,
            ChecksumSha256 = input.ChecksumSha256.Trim().ToLowerInvariant(),
            Status = BackupStatus.Verified,
            RetentionUntil = input.RetentionUntil,
            Detail = input.Detail.Trim(),
        };

        await store.AddBackupAsync(backup, cancellationToken);

        return new OperationsCommandResult(
            OperationsCommandStatus.Created,
            Backup: ToView(backup));
    }

    /// <summary>Lists the backups of the product, the newest first.</summary>
    public async Task<OperationsCommandResult> ListBackupsAsync(
        OperationsActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden();
        }

        var backups = await store.ListBackupsAsync(cancellationToken);

        return new OperationsCommandResult(
            OperationsCommandStatus.Succeeded,
            Backups: backups.Select(ToView).ToArray());
    }

    /// <summary>Lists the restore rehearsals, the newest first.</summary>
    public async Task<OperationsCommandResult> ListRestoreRehearsalsAsync(
        OperationsActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden();
        }

        var rehearsals = await store.ListRestoreRehearsalsAsync(cancellationToken);

        return new OperationsCommandResult(
            OperationsCommandStatus.Succeeded,
            Rehearsals: rehearsals.Select(ToView).ToArray());
    }

    /// <summary>Lists the deletion requests, the newest first.</summary>
    public async Task<OperationsCommandResult> ListDeletionRequestsAsync(
        OperationsActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden();
        }

        var requests = await store.ListDeletionRequestsAsync(cancellationToken);

        return new OperationsCommandResult(
            OperationsCommandStatus.Succeeded,
            Deletions: requests.Select(ToView).ToArray());
    }

    /// <summary>
    /// Runs a restore rehearsal. The rehearsal dumps the live database, restores
    /// the dump into an isolated database of its own, runs the four checks
    /// against that database and drops the isolated database again.
    ///
    /// The rehearsal is its own command. It never runs inside the transaction of
    /// another command and it never writes a ledger row.
    /// </summary>
    public async Task<OperationsCommandResult> RunRestoreRehearsalAsync(
        OperationsActor actor,
        RunRestoreRehearsalInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden();
        }

        if (input.BrowserJourneys is < 0 || input.AxeRuns is < 0)
        {
            return new OperationsCommandResult(
                OperationsCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["browser_journeys"] = ["Use zero or a positive number."],
                    ["axe_runs"] = ["Use zero or a positive number."],
                },
                Message: "The rehearsal could not start because some fields are not valid.");
        }

        if (input.BackupId is not null)
        {
            var backup = await store.FindBackupAsync(input.BackupId.Value, cancellationToken);

            if (backup is null)
            {
                return NotFound("The backup does not exist.");
            }
        }

        var startedAt = clock.UtcNow;
        var tables = await measurement.ListTablesAsync(cancellationToken);
        var before = await ReadCountsAsync(measurement, tables, cancellationToken);

        string? isolated = null;
        var notes = new List<string>();
        var schemaCheck = "the rehearsal did not run";
        var dataCheck = "the rehearsal did not run";
        var objectCheck = "the rehearsal did not run";
        var ledgerCheck = "the rehearsal did not run";
        var passed = false;

        try
        {
            isolated = await environment.PrepareAsync(cancellationToken);

            using var restored = environment.OpenDatabase(isolated);

            var after = await ReadCountsAsync(measurement, tables, cancellationToken);
            var restoredCounts = await ReadCountsAsync(restored, tables, cancellationToken);
            var restoredTables = await restored.ListTablesAsync(cancellationToken);

            var volatileTables = tables
                .Where(table => before[table] != after[table])
                .ToArray();

            var missingTables = restoredTables
                .Where(table => !tables.Contains(table))
                .ToArray();

            var differingTables = tables
                .Where(table => !volatileTables.Contains(table) && restoredCounts[table] != after[table])
                .ToArray();

            var schema = await restored.ReadSchemaAsync(cancellationToken);
            var sourceObjects = await measurement.ReadObjectInventoryAsync(cancellationToken);
            var restoredObjects = await restored.ReadObjectInventoryAsync(cancellationToken);
            var ledger = await restored.ReconcileLedgerAsync(cancellationToken);

            schemaCheck = DescribeSchema(schema);
            dataCheck = DescribeData(tables.Count, volatileTables, differingTables, missingTables);
            objectCheck = DescribeObjects(sourceObjects, restoredObjects);
            ledgerCheck = DescribeLedger(ledger);

            if (volatileTables.Length > 0)
            {
                notes.Add(
                    $"{volatileTables.Length} tables changed while the rehearsal ran; their row counts were not compared.");
            }

            var objectsMatch = sourceObjects.Count == restoredObjects.Count
                && string.Equals(sourceObjects.ChecksumSha256, restoredObjects.ChecksumSha256, StringComparison.Ordinal);

            passed = schema.MatchesExpected
                && differingTables.Length == 0
                && missingTables.Length == 0
                && objectsMatch
                && ledger.UnbalancedTransactions == 0
                && ledger.NegativeOwnerAccounts == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The operator gets a failed rehearsal and the isolated database is
            // dropped. The reason stays in the log of the server and it never
            // reaches the answer of the API.
            notes.Add("the rehearsal could not be completed");
        }
        finally
        {
            if (isolated is not null)
            {
                // The isolated database is dropped even when the request was
                // cancelled, so no rehearsal leaves a database behind.
                await environment.DropAsync(isolated, CancellationToken.None);
            }
        }

        var rehearsal = new RestoreRehearsal
        {
            Id = Guid.NewGuid(),
            BackupId = input.BackupId,
            Namespace = isolated ?? "not created",
            StartedAt = startedAt,
            CompletedAt = clock.UtcNow,
            Status = passed ? RestoreRehearsalStatus.Passed : RestoreRehearsalStatus.Failed,
            SchemaCheck = schemaCheck,
            DataCheck = dataCheck,
            ObjectCheck = objectCheck,
            LedgerCheck = ledgerCheck,
            Notes = string.Join(" ", notes),
            BrowserJourneys = input.BrowserJourneys,
            AxeRuns = input.AxeRuns,
        };

        await store.AddRestoreRehearsalAsync(rehearsal, cancellationToken);

        return new OperationsCommandResult(
            OperationsCommandStatus.Succeeded,
            Rehearsal: ToView(rehearsal));
    }

    /// <summary>
    /// Handles a deletion request of an administrator. The identity of the
    /// subject is removed, the account keeps a pseudonym and the account is
    /// closed. The settled financial records keep the pseudonym and they never
    /// keep the identity of the subject.
    /// </summary>
    public async Task<OperationsCommandResult> HandleDeletionRequestAsync(
        OperationsActor actor,
        DeletionRequestInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden();
        }

        if (input.SubjectUserId == Guid.Empty)
        {
            return new OperationsCommandResult(
                OperationsCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["subject_user_id"] = ["Name the account the request is about."],
                },
                Message: "The request could not be handled because some fields are not valid.");
        }

        if (input.Scope.Trim().Length > ScopeMaxLength)
        {
            return new OperationsCommandResult(
                OperationsCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["scope"] = [$"Use at most {ScopeMaxLength} characters."],
                },
                Message: "The request could not be handled because some fields are not valid.");
        }

        var user = await identities.FindUserAsync(input.SubjectUserId, cancellationToken);

        if (user is null)
        {
            return NotFound("The account of the request does not exist.");
        }

        var now = clock.UtcNow;
        var pseudonym = BuildPseudonym();

        user.Username = pseudonym;
        user.DisplayName = string.Empty;
        user.Email = string.Empty;
        user.Bio = string.Empty;
        user.Skills = [];
        user.Languages = [];
        user.GithubLogin = string.Empty;
        user.IsPublic = false;
        user.UpdatedAt = now;

        if (!await identities.TrySaveUserAsync(user, cancellationToken))
        {
            return new OperationsCommandResult(
                OperationsCommandStatus.Unavailable,
                Message: "The account changed while the request was handled. Read the request again and try once more.");
        }

        // The account is closed: the provider identity is gone and every
        // session of the account is revoked, so the account can never sign in
        // again. The rows of the ledger and of the audit keep the identifier
        // of the account, and the account keeps only the pseudonym.
        await identities.RemoveExternalIdentityAsync(user.Id, cancellationToken);
        await identities.RevokeSessionsAsync(user.Id, now, cancellationToken);

        var requestId = Guid.NewGuid();

        var request = new DeletionRequest
        {
            Id = requestId,
            RequesterId = actor.UserId!.Value,
            SubjectUserId = user.Id,
            Scope = string.IsNullOrWhiteSpace(input.Scope) ? "identity" : input.Scope.Trim(),
            ReceivedAt = now,
            Status = DeletionRequestStatus.Completed,
            PseudonymizedUserId = pseudonym,
            AuditReference = $"operations_deletion_request:{requestId}",
        };

        await store.AddDeletionRequestAsync(request, cancellationToken);

        return new OperationsCommandResult(
            OperationsCommandStatus.Created,
            Deletion: ToView(request));
    }

    /// <summary>
    /// Reads the gate of the pilot. Every value is measured now: the counts of
    /// the database and the numbers the operator recorded. A target that has no
    /// observation window yet is reported as "target, not yet measured" and it
    /// never counts as passed.
    /// </summary>
    public async Task<PilotGateView> GetPilotGateAsync(
        OperationsActor actor,
        CancellationToken cancellationToken)
    {
        var rehearsals = await store.ListRestoreRehearsalsAsync(cancellationToken);
        var latest = rehearsals.Count > 0 ? rehearsals[0] : null;

        var accepted = await measurement.MeasureAsync(OperationsGateCommands.AcceptedContributions, cancellationToken);
        var openCases = await measurement.MeasureAsync(OperationsGateCommands.OpenCases, cancellationToken);
        var deadLettered = await measurement.MeasureAsync(OperationsGateCommands.DeadLetteredDeliveries, cancellationToken);

        var items = new List<PilotGateItemView>
        {
            new(
                "accepted_contributions",
                accepted.ToString(),
                "at least 1",
                accepted >= 1,
                OperationsGateCommands.AcceptedContributions),
            new(
                "open_cases",
                openCases.ToString(),
                "0",
                openCases == 0,
                OperationsGateCommands.OpenCases),
            new(
                "dead_lettered_deliveries",
                deadLettered.ToString(),
                "0",
                deadLettered == 0,
                OperationsGateCommands.DeadLetteredDeliveries),
            new(
                "browser_journeys",
                DescribeOperatorCount(latest?.BrowserJourneys),
                "at least 1",
                latest?.BrowserJourneys >= 1,
                OperationsGateCommands.BrowserJourneys),
            new(
                "axe_runs",
                DescribeOperatorCount(latest?.AxeRuns),
                "at least 1",
                latest?.AxeRuns >= 1,
                OperationsGateCommands.AxeRuns),
            new(
                "restore_rehearsal",
                latest is null ? "not yet run" : latest.Status.ToString().ToLowerInvariant(),
                "passed",
                latest?.Status == RestoreRehearsalStatus.Passed,
                OperationsGateCommands.RestoreRehearsal),
            new(
                "api_latency_p95",
                "target, not yet measured",
                "below 500 ms",
                false,
                OperationsGateCommands.LatencyProbe),
            new(
                "availability",
                "target, not yet measured",
                "99.5 percent over a documented observation window",
                false,
                OperationsGateCommands.AvailabilityProbe),
        };

        var ready = items.All(item => item.Passed);

        return new PilotGateView(items, ready, clock.UtcNow);
    }

    /// <summary>
    /// Reads the public status of the product. It holds the state of the runtime
    /// and of its dependencies, the measured counts of the marketplace and the
    /// honest statement of what is not implemented yet. It never holds a
    /// private field, an identity of a person or the amount of the ledger of a
    /// person.
    /// </summary>
    public async Task<PublicStatusView> GetPublicStatusAsync(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains(ReadyTag),
            cancellationToken);

        var dependencies = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new PublicStatusDependency(
                entry.Key,
                entry.Value.Status == HealthStatus.Healthy ? "up" : "down",
                entry.Value.Description ?? string.Empty))
            .ToArray();

        var published = await measurement.MeasureAsync(OperationsGateCommands.PublishedTasks, cancellationToken);
        var accepted = await measurement.MeasureAsync(OperationsGateCommands.AcceptedContributions, cancellationToken);
        var openCases = await measurement.MeasureAsync(OperationsGateCommands.OpenCases, cancellationToken);
        var contributors = await measurement.MeasureAsync(OperationsGateCommands.Contributors, cancellationToken);

        return new PublicStatusView(
            report.Status == HealthStatus.Healthy ? "available" : "unavailable",
            clock.UtcNow,
            dependencies,
            new PublicStatusMarketplace(published, accepted, openCases, contributors),
            [
                "Cash payments are not implemented. The credits of the marketplace are not money.",
                "A native mobile application is not implemented.",
                "The product does not execute the work of a seat with an AI.",
                "The product does not synchronise with an external issue tracker.",
                "The product does not deploy itself. The deployment runs through the runbooks of the operator.",
            ]);
    }

    private static Dictionary<string, string[]> ValidateBackup(RecordBackupInput input)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(input.Location) || input.Location.Trim().Length > 200)
        {
            errors["location"] = ["Name the place of the backup with at most 200 characters."];
        }

        if (input.SizeBytes < 0)
        {
            errors["size_bytes"] = ["Use zero or a positive number of bytes."];
        }

        if (!IsChecksum(input.ChecksumSha256))
        {
            errors["checksum_sha256"] = ["Use the 64 characters of a SHA-256 checksum in hex."];
        }

        if (input.Detail.Trim().Length > DetailMaxLength)
        {
            errors["detail"] = [$"Use at most {DetailMaxLength} characters."];
        }

        return errors;
    }

    private static bool IsChecksum(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length != 64)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<Dictionary<string, long>> ReadCountsAsync(
        IOperationsDatabase database,
        IReadOnlyList<string> tables,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var table in tables)
        {
            counts[table] = await database.CountRowsAsync(table, cancellationToken);
        }

        return counts;
    }

    private static string DescribeSchema(RehearsalSchema schema) =>
        schema.MatchesExpected
            ? $"the restored database holds the expected schema {schema.AppliedVersion}"
            : $"the restored database holds the schema {schema.AppliedVersion} and it is not the expected one";

    private static string DescribeData(
        int tableCount,
        IReadOnlyList<string> volatileTables,
        IReadOnlyList<string> differingTables,
        IReadOnlyList<string> missingTables)
    {
        if (missingTables.Count > 0)
        {
            return $"the restored database holds {missingTables.Count} tables the live database does not hold";
        }

        if (differingTables.Count > 0)
        {
            return $"the row counts of {differingTables.Count} of {tableCount} tables differ from the live database";
        }

        return $"the row counts of {tableCount} tables match the live database";
    }

    private static string DescribeObjects(RehearsalObjectInventory source, RehearsalObjectInventory restored)
    {
        var same = source.Count == restored.Count
            && string.Equals(source.ChecksumSha256, restored.ChecksumSha256, StringComparison.Ordinal);

        return same
            ? $"the restored database refers to {restored.Count} objects and the checksum of their keys matches the live database"
            : $"the restored database refers to {restored.Count} objects and the checksum of their keys does not match the live database";
    }

    private static string DescribeLedger(RehearsalLedger ledger) =>
        ledger.UnbalancedTransactions == 0 && ledger.NegativeOwnerAccounts == 0
            ? $"the {ledger.Transactions} transactions of the journal sum to zero and no account with an owner holds a negative balance"
            : $"the {ledger.Transactions} transactions of the journal hold {ledger.UnbalancedTransactions} sums that do not add up to zero and {ledger.NegativeOwnerAccounts} accounts with an owner hold a negative balance";

    private static string DescribeOperatorCount(int? count) =>
        count.HasValue ? count.Value.ToString() : "target, not yet measured";

    /// <summary>
    /// Builds the pseudonym the subject of a deletion keeps. It is a name that
    /// holds no part of the identity of the subject.
    /// </summary>
    private static string BuildPseudonym() =>
        $"deleted-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}";

    private static OperationsCommandResult Forbidden() =>
        new(
            OperationsCommandStatus.Forbidden,
            Message: "This area belongs to the administrators of the product.");

    private static OperationsCommandResult NotFound(string message) =>
        new(OperationsCommandStatus.NotFound, Message: message);

    private static BackupView ToView(BackupRecord backup) =>
        new(
            backup.Id,
            backup.TakenAt,
            backup.Kind.ToString().ToLowerInvariant(),
            backup.Location,
            backup.SizeBytes,
            backup.ChecksumSha256,
            backup.Status.ToString().ToLowerInvariant(),
            backup.RetentionUntil,
            backup.Detail);

    private static RestoreRehearsalView ToView(RestoreRehearsal rehearsal) =>
        new(
            rehearsal.Id,
            rehearsal.BackupId,
            rehearsal.Namespace,
            rehearsal.StartedAt,
            rehearsal.CompletedAt,
            rehearsal.Status.ToString().ToLowerInvariant(),
            rehearsal.SchemaCheck,
            rehearsal.DataCheck,
            rehearsal.ObjectCheck,
            rehearsal.LedgerCheck,
            rehearsal.Notes,
            rehearsal.BrowserJourneys,
            rehearsal.AxeRuns);

    private static DeletionRequestView ToView(DeletionRequest request) =>
        new(
            request.Id,
            request.ReceivedAt,
            request.Scope,
            request.Status.ToString().ToLowerInvariant(),
            request.PseudonymizedUserId,
            request.AuditReference);
}
