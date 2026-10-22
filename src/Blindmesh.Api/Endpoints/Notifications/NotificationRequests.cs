namespace Blindmesh.Api.Endpoints.Notifications;

/// <summary>
/// The body of a change of the preferences of the notifications. A field that
/// is left out keeps its value. The body holds no credential.
/// </summary>
public sealed record UpdateNotificationPreferencesRequest
{
    public bool? InAppEnabled { get; init; }

    public bool? EmailEnabled { get; init; }
}
