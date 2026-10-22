using Blindmesh.Domain.Notifications;

namespace Blindmesh.Application.Notifications;

/// <summary>
/// The persistence of the notification module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly. The store is the only place that writes a delivery row. The
/// module writes no ledger row: a notification never moves credits.
/// </summary>
public interface INotificationStore
{
    /// <summary>
    /// The delivery of one identity. Null when the identity was never written,
    /// so a redelivered event never writes a second row.
    /// </summary>
    Task<NotificationDelivery?> FindByUniqueKeyAsync(string uniqueKey, CancellationToken cancellationToken);

    /// <summary>Writes one delivery of the queue.</summary>
    Task AddDeliveryAsync(NotificationDelivery delivery, CancellationToken cancellationToken);

    /// <summary>The delivery of one id, when it exists.</summary>
    Task<NotificationDelivery?> FindDeliveryAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The deliveries of one recipient, newest first. The page starts after the
    /// cursor, so the next page is read in the same stable order.
    /// </summary>
    Task<IReadOnlyList<NotificationDelivery>> ListDeliveriesAsync(
        Guid recipientId,
        NotificationCursor? cursor,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// The deliveries that may be sent now. The rows are locked with FOR UPDATE
    /// SKIP LOCKED, so two workers never claim the same delivery.
    /// </summary>
    Task<IReadOnlyList<NotificationDelivery>> ClaimDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Marks a delivery as sent and stores the moment it went out.</summary>
    Task MarkSentAsync(NotificationDelivery delivery, DateTimeOffset sentAt, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a delivery as failed and stores the moment of the next attempt.
    /// The reason is redacted before it is stored.
    /// </summary>
    Task MarkFailedAsync(
        NotificationDelivery delivery,
        DateTimeOffset failedAt,
        DateTimeOffset? nextAttemptAt,
        string? error,
        CancellationToken cancellationToken);

    /// <summary>Marks a delivery as dead lettered. It never goes out again.</summary>
    Task MarkDeadLetteredAsync(
        NotificationDelivery delivery,
        DateTimeOffset deadLetteredAt,
        string? error,
        CancellationToken cancellationToken);

    /// <summary>The preferences of one recipient, when they were written.</summary>
    Task<NotificationPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes the preferences of one recipient.</summary>
    Task AddPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken);

    /// <summary>The accounts that administer the product.</summary>
    Task<IReadOnlyList<Guid>> ListAdministratorIdsAsync(CancellationToken cancellationToken);

    /// <summary>Saves the changed rows of the module.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
