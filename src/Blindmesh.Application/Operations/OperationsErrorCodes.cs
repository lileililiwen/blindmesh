namespace Blindmesh.Application.Operations;

/// <summary>
/// The error codes of the operations module. They name the reason of a refusal
/// without naming a provider, a SQL statement or a credential.
/// </summary>
public static class OperationsErrorCodes
{
    /// <summary>A signed in account is required.</summary>
    public const string SessionRequired = "session_required";

    /// <summary>The account may not run the command.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The record does not exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>The request body is not valid.</summary>
    public const string BadRequest = "bad_request";

    /// <summary>The command could not run right now.</summary>
    public const string Unavailable = "unavailable";
}
