namespace Blindmesh.Application.Moderation;

/// <summary>
/// The error codes of the moderation module. They are part of the contract of
/// the API and never carry a detail of the database, of the provider or of the
/// ledger.
/// </summary>
public static class ModerationErrorCodes
{
    public const string BadRequest = "bad_request";

    public const string NotFound = "not_found";

    public const string Forbidden = "forbidden";

    public const string Unprocessable = "unprocessable_entity";

    public const string Conflict = "conflict";

    public const string VersionConflict = "version_conflict";

    public const string InvalidTransition = "invalid_transition";

    public const string StateConflict = "state_conflict";

    public const string SessionRequired = "session_required";

    /// <summary>The account of the request is banned.</summary>
    public const string AccountBanned = "account_banned";

    /// <summary>The award of the dispute cannot be reversed any more.</summary>
    public const string ArbitrationNotReversible = "arbitration_not_reversible";

    /// <summary>The case already carries the only appeal it may carry.</summary>
    public const string AppealAlreadyUsed = "appeal_already_used";

    /// <summary>The window of the appeal has passed.</summary>
    public const string AppealWindowPassed = "appeal_window_passed";

    /// <summary>The dispute of the submission is already open.</summary>
    public const string DisputeAlreadyOpen = "dispute_already_open";
}
