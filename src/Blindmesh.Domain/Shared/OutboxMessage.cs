namespace Blindmesh.Domain.Shared;

/// <summary>
/// An event produced inside a product transaction and published after the commit.
/// The producer owns the payload; consumers re-check recipient access.
/// </summary>
public sealed record OutboxMessage
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required int Version { get; init; }

    public Guid? ActorId { get; init; }

    public required Guid AggregateId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>JSON payload owned by the producer.</summary>
    public required string Payload { get; init; }
}
