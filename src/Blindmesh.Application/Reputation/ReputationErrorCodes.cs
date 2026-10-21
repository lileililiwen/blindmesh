namespace Blindmesh.Application.Reputation;

/// <summary>
/// The error codes of the reputation module. They follow the error envelope of
/// the API and never carry a provider, a SQL or a credential detail.
/// </summary>
public static class ReputationErrorCodes
{
    public const string NotFound = "not_found";

    public const string SessionRequired = "session_required";

    public const string Forbidden = "forbidden";

    public const string ServiceUnavailable = "service_unavailable";
}
