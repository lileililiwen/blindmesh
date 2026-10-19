namespace Blindmesh.Application.Abstractions;

/// <summary>
/// UTC clock seam. Every timestamp written by the product comes from this seam so
/// tests can freeze time deterministically.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
