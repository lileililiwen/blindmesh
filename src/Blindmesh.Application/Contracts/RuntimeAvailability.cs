namespace Blindmesh.Application.Contracts;

/// <summary>
/// Availability of the runtime services. Only real, measured dependency states are
/// reported; the payload contains no synthetic counters.
/// </summary>
public sealed record RuntimeAvailability
{
    public required string Status { get; init; }

    public required DateTimeOffset CheckedAt { get; init; }

    public required IReadOnlyList<RuntimeDependency> Dependencies { get; init; }
}

public sealed record RuntimeDependency
{
    public required string Name { get; init; }

    public required string Status { get; init; }

    public string? Detail { get; init; }
}
