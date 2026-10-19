namespace Blindmesh.Infrastructure.Persistence.Entities;

/// <summary>
/// Durable outbox row. A row is inserted inside the transaction of the command
/// that produced the event and is claimed later by the worker.
/// </summary>
public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public int Version { get; set; }

    public Guid? ActorId { get; set; }

    public Guid AggregateId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
