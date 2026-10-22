namespace Blindmesh.Application.Notifications;

/// <summary>
/// The error codes of the notification module. They follow the error envelope of
/// the API and never carry a provider, a SQL or a credential detail.
/// </summary>
public static class NotificationErrorCodes
{
    public const string NotFound = "not_found";

    public const string SessionRequired = "session_required";

    public const string Forbidden = "forbidden";

    public const string BadRequest = "bad_request";

    public const string ServiceUnavailable = "service_unavailable";
}
