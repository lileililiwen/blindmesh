namespace Blindmesh.Application.Identity;

/// <summary>
/// The error codes of the identity module. They are part of the published
/// contract, so a client can react to a known reason without reading the
/// message.
/// </summary>
public static class IdentityErrorCodes
{
    public const string SessionRequired = "session_required";

    public const string SessionInvalid = "session_invalid";

    public const string CsrfTokenInvalid = "csrf_token_invalid";

    public const string Forbidden = "forbidden";

    public const string NotFound = "not_found";

    public const string VersionConflict = "version_conflict";

    public const string InvalidState = "invalid_oauth_state";

    public const string ExpiredState = "expired_oauth_state";

    public const string ReplayedState = "replayed_oauth_state";

    public const string MismatchedState = "mismatched_oauth_state";

    public const string ProviderUnavailable = "provider_unavailable";

    public const string Unprocessable = "unprocessable_entity";
}
