namespace Blindmesh.Application.Notifications;

/// <summary>
/// One notification of the signed in recipient. It holds the safe summary and
/// the relative deep link of the source; it never holds a title of a private
/// project, a finding text, a party or an amount.
/// </summary>
public sealed record NotificationView(
    Guid Id,
    string Category,
    string Label,
    string Summary,
    string DeepLink,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

/// <summary>
/// The preferences of the signed in recipient. Both channels are on when the
/// recipient never changed them.
/// </summary>
public sealed record NotificationPreferenceView(
    bool InAppEnabled,
    bool EmailEnabled,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// The change of the preferences of the signed in recipient. A field that is
/// left out keeps its value.
/// </summary>
public sealed record NotificationPreferenceInput(
    bool? InAppEnabled,
    bool? EmailEnabled);

/// <summary>
/// The outcome of the change of the preferences. It holds the committed state,
/// so the interface can announce the saved state only after the server
/// confirmed it.
/// </summary>
public sealed record NotificationPreferenceResult(
    NotificationPreferenceStatus Status,
    NotificationPreferenceView Preferences);

/// <summary>The outcome of a change of the preferences.</summary>
public enum NotificationPreferenceStatus
{
    /// <summary>The preferences were written.</summary>
    Updated,

    /// <summary>The preferences already held the wanted values.</summary>
    Unchanged,
}

/// <summary>The outcome of the marking of one notification as read.</summary>
public sealed record NotificationReadResult(
    NotificationReadStatus Status,
    NotificationView? Notification);

/// <summary>
/// The outcome of the writing of one notification. The count is the number of
/// rows that were really written; a redelivered event writes none.
/// </summary>
public sealed record NotificationEnqueueResult(int Created);

/// <summary>The outcome of the marking of one notification as read.</summary>
public enum NotificationReadStatus
{
    /// <summary>The notification belongs to the caller and is now read.</summary>
    Read,

    /// <summary>The notification was already read.</summary>
    AlreadyRead,

    /// <summary>The notification does not belong to the caller. The answer
    /// carries no summary and no identity of the caller.</summary>
    NotFound,
}
