using Blindmesh.Application.Notifications;
using Blindmesh.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Worker;

/// <summary>
/// Sends the notifications that are due. The deliveries are the queue of the
/// notification module and they hold their own state: a delivery that fails
/// waits for its next attempt and never blocks the other deliveries. A
/// delivery that used every attempt is dead lettered and an operator alert
/// names it.
/// </summary>
public sealed class NotificationDeliveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDeliveryService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(3);

    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sent = await ProcessBatchAsync(stoppingToken);

                if (sent == 0)
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
                logger.LogError(exception, "The notification delivery cycle failed.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var claimed = await notifications.ClaimDueAsync(BatchSize, cancellationToken);
        var sent = 0;

        foreach (var delivery in claimed)
        {
            try
            {
                if (await notifications.DeliverAsync(delivery, cancellationToken))
                {
                    sent += 1;
                }
                else if (delivery.Status == NotificationStatus.DeadLettered)
                {
                    // The line names the delivery, the event and the recipient.
                    // It never holds a provider detail, a host or a credential.
                    logger.LogWarning(
                        "The notification delivery {DeliveryId} of the event {EventType} for the recipient {RecipientId} failed {Attempts} times and is dead lettered.",
                        delivery.Id,
                        delivery.EventType,
                        delivery.RecipientId,
                        delivery.AttemptCount);
                }
                else if (delivery.Status == NotificationStatus.Failed)
                {
                    // The outcome of a failed attempt of the mail channel is
                    // uncertain: the message may still reach the recipient, so
                    // it can arrive more than once. The line never claims that
                    // the email arrives exactly once.
                    logger.LogWarning(
                        "The attempt {Attempts} of the notification delivery {DeliveryId} for the recipient {RecipientId} failed. The next attempt is at {NextAttemptAt}. The message may still reach the recipient, so it can arrive more than once.",
                        delivery.AttemptCount,
                        delivery.Id,
                        delivery.RecipientId,
                        delivery.NextAttemptAt);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "The notification delivery {DeliveryId} could not be processed.",
                    delivery.Id);
            }
        }

        return sent;
    }
}
