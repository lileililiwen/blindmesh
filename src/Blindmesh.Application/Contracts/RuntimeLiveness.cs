namespace Blindmesh.Application.Contracts;

/// <summary>Liveness of the process. It reports no dependency state.</summary>
public sealed record RuntimeLiveness
{
    public required string Status { get; init; }

    public required DateTimeOffset CheckedAt { get; init; }
}
