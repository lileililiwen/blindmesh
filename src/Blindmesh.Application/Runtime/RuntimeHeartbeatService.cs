using System.Text.Json;
using Blindmesh.Application.Abstractions;
using Blindmesh.Domain.Shared;

namespace Blindmesh.Application.Runtime;

/// <summary>
/// Records that the running shell accepted a command. The outbox message is
/// written inside the caller's transaction, so a committed heartbeat always has a
/// durable event and a rolled back command leaves no event behind.
/// </summary>
public sealed class RuntimeHeartbeatService(IOutboxWriter outboxWriter, IClock clock)
{
    public async Task<RuntimeHeartbeatResult> RecordAsync(Guid? actorId, string? note, CancellationToken cancellationToken)
    {
        var messageId = Guid.NewGuid();
        var occurredAt = clock.UtcNow;
        var payload = JsonSerializer.Serialize(new RuntimeHeartbeatPayload(messageId, note, occurredAt));

        await outboxWriter.EnqueueAsync(
            new OutboxMessage
            {
                Id = messageId,
                Type = OutboxEventTypes.RuntimeHeartbeat,
                Version = 1,
                ActorId = actorId,
                AggregateId = messageId,
                OccurredAt = occurredAt,
                Payload = payload,
            },
            cancellationToken);

        return new RuntimeHeartbeatResult(messageId, occurredAt, "accepted");
    }
}

public sealed record RuntimeHeartbeatPayload(Guid MessageId, string? Note, DateTimeOffset OccurredAt);

public sealed record RuntimeHeartbeatResult(Guid MessageId, DateTimeOffset OccurredAt, string Status);
