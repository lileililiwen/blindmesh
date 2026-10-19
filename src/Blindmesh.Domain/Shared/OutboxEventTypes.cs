namespace Blindmesh.Domain.Shared;

/// <summary>
/// Outbox event types owned by the runtime foundation package.
/// Business event types are introduced by the package that owns the aggregate.
/// </summary>
public static class OutboxEventTypes
{
    /// <summary>Records that the API accepted a runtime heartbeat command.</summary>
    public const string RuntimeHeartbeat = "runtime.heartbeat";
}
