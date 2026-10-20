namespace Blindmesh.Application.Tasks;

/// <summary>
/// The error codes of the tasks module. They are part of the contract of the
/// API and never carry a detail of the database or of a provider.
/// </summary>
public static class TaskErrorCodes
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

    public const string SessionInvalid = "session_invalid";

    public const string CsrfTokenInvalid = "csrf_token_invalid";

    public const string InsufficientFunds = "insufficient_funds";
}
