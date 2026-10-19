namespace Blindmesh.Application.Contracts;

/// <summary>
/// List envelope. <see cref="NextCursor"/> is null on the last page.
/// </summary>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public string? NextCursor { get; init; }
}
