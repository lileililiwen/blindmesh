namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The error codes of the capabilities module. They are part of the published
/// contract, so a client can react to a known reason without reading the
/// message.
/// </summary>
public static class CapabilityErrorCodes
{
    public const string SessionRequired = "session_required";

    public const string SessionInvalid = "session_invalid";

    public const string CsrfTokenInvalid = "csrf_token_invalid";

    public const string Forbidden = "forbidden";

    public const string NotFound = "not_found";

    public const string VersionConflict = "version_conflict";

    public const string StateConflict = "state_conflict";

    public const string Unprocessable = "unprocessable_entity";

    public const string BadRequest = "bad_request";
}
