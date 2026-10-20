using Blindmesh.Application.Abstractions;

namespace Blindmesh.Tests.Credits;

/// <summary>
/// The clock of a credit test. It starts at a fixed moment and can be moved
/// forward, so the expiry of a grant is exercised without waiting for it.
/// </summary>
internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}
