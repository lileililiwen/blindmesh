using Blindmesh.Application.Abstractions;
using Blindmesh.Domain.Shared;
using Blindmesh.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Outbox persistence. Messages are staged on the caller's DbContext and written
/// by the transaction that owns the command, so an event is never stored without
/// the state change that produced it.
/// </summary>
public sealed class OutboxRepository(BlindmeshDbContext dbContext) : IOutboxWriter
{
    public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = message.Id,
            Type = message.Type,
            Version = message.Version,
            ActorId = message.ActorId,
            AggregateId = message.AggregateId,
            OccurredAt = message.OccurredAt,
            Payload = message.Payload,
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Claims pending messages for the worker. The rows are locked with
    /// FOR UPDATE SKIP LOCKED, so two workers never claim the same message.
    /// </summary>
    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(int limit, CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .FromSqlRaw(
                """
                SELECT id, type, version, actor_id, aggregate_id, occurred_at, payload, processed_at, attempts, last_error
                FROM outbox_messages
                WHERE processed_at IS NULL
                ORDER BY occurred_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT {0}
                """,
                limit)
            .ToListAsync(cancellationToken);

        return messages;
    }

    public async Task MarkProcessedAsync(OutboxMessageEntity message, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        message.ProcessedAt = processedAt;
        message.Attempts += 1;
        message.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(OutboxMessageEntity message, string error, CancellationToken cancellationToken)
    {
        message.Attempts += 1;
        message.LastError = error.Length > 500 ? error[..500] : error;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
