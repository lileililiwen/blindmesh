using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Worker;

/// <summary>
/// Claims pending outbox events with FOR UPDATE SKIP LOCKED, applies the
/// registered handlers and commits the result in the same transaction. Two
/// workers never claim the same event, and an event that fails stays pending
/// for a later attempt.
/// </summary>
public sealed class OutboxClaimService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxClaimService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var handled = await ProcessBatchAsync(stoppingToken);

                if (handled == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The outbox claim cycle failed.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<BlindmeshDbContext>();
        var repository = provider.GetRequiredService<OutboxRepository>();
        // One event may have more than one consumer, so the handlers are
        // grouped by their event type.
        var handlers = provider.GetServices<IOutboxHandler>()
            .GroupBy(handler => handler.EventType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await repository.ClaimPendingAsync(20, cancellationToken);

        var applied = 0;

        foreach (var message in messages)
        {
            if (!handlers.TryGetValue(message.Type, out var matching))
            {
                logger.LogWarning(
                    "No handler is registered for outbox event {EventType} {MessageId}. The event stays pending.",
                    message.Type,
                    message.Id);

                continue;
            }

            var outboxMessage = new Domain.Shared.OutboxMessage
            {
                Id = message.Id,
                Type = message.Type,
                Version = message.Version,
                ActorId = message.ActorId,
                AggregateId = message.AggregateId,
                OccurredAt = message.OccurredAt,
                Payload = message.Payload,
            };

            try
            {
                foreach (var handler in matching)
                {
                    await handler.HandleAsync(outboxMessage, cancellationToken);
                }

                await repository.MarkProcessedAsync(message, DateTimeOffset.UtcNow, cancellationToken);
                applied += 1;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox event {EventType} {MessageId} failed.", message.Type, message.Id);
                await repository.MarkFailedAsync(message, exception.Message, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        // Only the messages that were really applied count. A message without a
        // handler stays pending, and counting it would keep the cycle busy for
        // ever while it waits for the package that will consume it.
        return applied;
    }
}
