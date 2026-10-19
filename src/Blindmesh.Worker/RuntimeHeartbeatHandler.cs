using Blindmesh.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Worker;

/// <summary>
/// Handles the runtime heartbeat event of the foundation. It has no business
/// meaning: it proves that a claimed event is dispatched and committed.
/// </summary>
public sealed class RuntimeHeartbeatHandler(ILogger<RuntimeHeartbeatHandler> logger) : IOutboxHandler
{
    public string EventType => OutboxEventTypes.RuntimeHeartbeat;

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handled outbox event {EventType} {MessageId} raised at {OccurredAt}.",
            message.Type,
            message.Id,
            message.OccurredAt);

        return Task.CompletedTask;
    }
}
