using Blindmesh.Application.Notifications;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Notifications;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Notifications;

/// <summary>
/// The persistence of the notification module. The store is scoped, so it
/// shares the scoped database context of the request or of the worker cycle.
/// The module writes no ledger row: a notification never moves credits.
/// </summary>
public sealed class EfNotificationStore(BlindmeshDbContext dbContext) : INotificationStore
{
    public Task<NotificationDelivery?> FindByUniqueKeyAsync(
        string uniqueKey,
        CancellationToken cancellationToken) =>
        dbContext.NotificationDeliveries.FirstOrDefaultAsync(
            delivery => delivery.UniqueKey == uniqueKey,
            cancellationToken);

    public Task AddDeliveryAsync(NotificationDelivery delivery, CancellationToken cancellationToken) =>
        dbContext.NotificationDeliveries.AddAsync(delivery, cancellationToken).AsTask();

    public Task<NotificationDelivery?> FindDeliveryAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.NotificationDeliveries.FirstOrDefaultAsync(
            delivery => delivery.Id == id,
            cancellationToken);

    public async Task<IReadOnlyList<NotificationDelivery>> ListDeliveriesAsync(
        Guid recipientId,
        NotificationCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        // The list holds the in-app notifications of the recipient. The email
        // delivery of an event is the copy that goes to the mailbox, so it is
        // never a second item of the list.
        var query = dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(delivery =>
                delivery.RecipientId == recipientId &&
                delivery.Channel == NotificationChannel.InApp);

        if (cursor is not null)
        {
            var createdAt = cursor.CreatedAt;
            var id = cursor.Id;

            query = query.Where(delivery =>
                delivery.CreatedAt < createdAt ||
                (delivery.CreatedAt == createdAt && delivery.Id < id));
        }

        var rows = await query
            .OrderByDescending(delivery => delivery.CreatedAt)
            .ThenByDescending(delivery => delivery.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows;
    }

    /// <summary>
    /// Claims the deliveries that may be sent now. The rows are locked with FOR
    /// UPDATE SKIP LOCKED, so two workers never claim the same delivery.
    /// </summary>
    public async Task<IReadOnlyList<NotificationDelivery>> ClaimDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var deliveries = await dbContext.NotificationDeliveries
            .FromSqlRaw(
                """
                SELECT id, recipient_id, event_type, category, channel, unique_key, subject, summary, deep_link,
                       status, attempt_count, next_attempt_at, message_id, last_error, created_at, sent_at, read_at,
                       dead_lettered_at
                FROM notification_deliveries
                WHERE status IN ('Pending', 'Failed', 'Sending') AND next_attempt_at <= {0}
                ORDER BY next_attempt_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT {1}
                """,
                now,
                limit)
            .ToListAsync(cancellationToken);

        return deliveries;
    }

    public async Task MarkSentAsync(
        NotificationDelivery delivery,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        delivery.Status = NotificationStatus.Sent;
        delivery.SentAt = sentAt;
        delivery.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        NotificationDelivery delivery,
        DateTimeOffset failedAt,
        DateTimeOffset? nextAttemptAt,
        string? error,
        CancellationToken cancellationToken)
    {
        delivery.Status = NotificationStatus.Failed;
        delivery.LastError = error;
        delivery.NextAttemptAt = nextAttemptAt ?? failedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDeadLetteredAsync(
        NotificationDelivery delivery,
        DateTimeOffset deadLetteredAt,
        string? error,
        CancellationToken cancellationToken)
    {
        delivery.Status = NotificationStatus.DeadLettered;
        delivery.DeadLetteredAt = deadLetteredAt;
        delivery.LastError = error;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<NotificationPreference?> FindPreferenceAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.FirstOrDefaultAsync(
            preference => preference.UserId == userId,
            cancellationToken);

    public Task AddPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.AddAsync(preference, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Guid>> ListAdministratorIdsAsync(CancellationToken cancellationToken)
    {
        var administrators = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Administrator)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        return administrators;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken) > 0;
}
