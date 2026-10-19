namespace Blindmesh.Application.Abstractions;

/// <summary>
/// Scoped idempotency records for repeatable commands. The scope is
/// actor + operation and the key is the caller supplied Idempotency-Key value.
/// The canonical payload hash and the original response are stored in the same
/// transaction as the effects of the command.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string scope, string key, CancellationToken cancellationToken);

    Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}

/// <summary>A stored command outcome that can be replayed for the same key and payload.</summary>
public sealed record IdempotencyRecord
{
    public required string Scope { get; init; }

    public required string Key { get; init; }

    public required string PayloadHash { get; init; }

    public required int StatusCode { get; init; }

    public required string ResponseBody { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
