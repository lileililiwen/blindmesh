namespace Blindmesh.Infrastructure.Persistence.Entities;

/// <summary>
/// Stored outcome of a repeatable command. The record is written in the same
/// transaction as the effects, so a replay never observes a partial commit.
/// </summary>
public sealed class IdempotencyRecordEntity
{
    public Guid Id { get; set; }

    /// <summary>actor + operation.</summary>
    public string Scope { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
