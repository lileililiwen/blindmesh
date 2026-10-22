namespace Blindmesh.Application.Matching;

/// <summary>
/// The error codes of the matching module. They are part of the contract of
/// the API and never carry a detail of the database, of a provider or of a
/// candidate.
/// </summary>
public static class MatchingErrorCodes
{
    public const string NotFound = "not_found";

    public const string Forbidden = "forbidden";

    public const string SessionRequired = "session_required";

    public const string SessionInvalid = "session_invalid";
}
