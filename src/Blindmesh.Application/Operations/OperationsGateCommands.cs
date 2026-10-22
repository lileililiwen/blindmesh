namespace Blindmesh.Application.Operations;

/// <summary>
/// The commands of the pilot gate. They are the exact commands that produced
/// the measured values of the gate, so an operator can run them again by hand
/// and see the same number.
///
/// The commands hold no value of a request and no credential. They are
/// constants of the product.
/// </summary>
public static class OperationsGateCommands
{
    /// <summary>The accepted contributions of the marketplace.</summary>
    public const string AcceptedContributions =
        "SELECT count(*)::bigint AS \"Value\" FROM submissions WHERE state = 'Accepted'";

    /// <summary>The cases of the arbitration that wait for a moderator.</summary>
    public const string OpenCases =
        "SELECT count(*)::bigint AS \"Value\" FROM disputes WHERE state IN ('Open', 'EvidenceRequested')";

    /// <summary>The deliveries of the notifications that never went out.</summary>
    public const string DeadLetteredDeliveries =
        "SELECT count(*)::bigint AS \"Value\" FROM notification_deliveries WHERE status = 'DeadLettered'";

    /// <summary>The tasks of the marketplace that are not a draft.</summary>
    public const string PublishedTasks =
        "SELECT count(*)::bigint AS \"Value\" FROM tasks WHERE status NOT IN ('Draft', 'Cancelled')";

    /// <summary>The accounts of the product.</summary>
    public const string Contributors =
        "SELECT count(*)::bigint AS \"Value\" FROM users";

    /// <summary>
    /// The probe that measures the response time of the API. The product does
    /// not collect the response time yet, so the gate reports this command and
    /// no measurement.
    /// </summary>
    public const string LatencyProbe =
        "for i in $(seq 1 200); do curl -s -o /dev/null -w \"%{time_total}\\n\" \"$BLINDMESH_API_URL/v1/health/ready\"; done | sort -n | sed -n '190p'";

    /// <summary>
    /// The probe that measures the availability of the API. The product does
    /// not collect the probes yet, so the gate reports this command and no
    /// measurement.
    /// </summary>
    public const string AvailabilityProbe =
        "curl -s -o /dev/null -w \"%{http_code}\\n\" \"$BLINDMESH_API_URL/v1/health/ready\"";

    /// <summary>The command of the browser journeys of the pilot.</summary>
    public const string BrowserJourneys =
        "npm --prefix apps/web run test:e2e -- --project=chromium --grep OPS";

    /// <summary>The command of the runs of axe of the pilot.</summary>
    public const string AxeRuns =
        "npm --prefix apps/web run test:a11y -- --grep OPS";

    /// <summary>The command of the restore rehearsal of the pilot.</summary>
    public const string RestoreRehearsal = "POST /v1/admin/operations/restores";
}
