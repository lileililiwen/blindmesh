namespace Blindmesh.Application.Credits;

/// <summary>
/// The error codes of the credits module. They are part of the published
/// contract, so a client can react to a known reason without reading the
/// message.
/// </summary>
public static class CreditErrorCodes
{
    public const string SessionRequired = "session_required";

    public const string SessionInvalid = "session_invalid";

    public const string CsrfTokenInvalid = "csrf_token_invalid";

    public const string Forbidden = "forbidden";

    public const string NotFound = "not_found";

    public const string Conflict = "conflict";

    public const string GrantNotEligible = "grant_not_eligible";

    public const string GrantAlreadyClaimed = "grant_already_claimed";

    public const string GrantLimitReached = "grant_limit_reached";

    public const string Unprocessable = "unprocessable_entity";

    public const string BadRequest = "bad_request";
}
