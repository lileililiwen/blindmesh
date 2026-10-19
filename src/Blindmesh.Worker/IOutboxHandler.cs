using Blindmesh.Domain.Shared;

namespace Blindmesh.Worker;

/// <summary>
/// Handles one outbox event type. The worker owns the dispatch; a handler only
/// applies the work of the package that owns the event.
/// </summary>
public interface IOutboxHandler
{
    string EventType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}
