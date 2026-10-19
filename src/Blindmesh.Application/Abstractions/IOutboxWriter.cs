using Blindmesh.Domain.Shared;

namespace Blindmesh.Application.Abstractions;

/// <summary>
/// Writes outbox messages inside the caller's transaction. The message is stored
/// atomically with the state change that produced it; it is never sent to a
/// broker from inside a monetary transaction.
/// </summary>
public interface IOutboxWriter
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken);
}
