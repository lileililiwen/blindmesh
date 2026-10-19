using Blindmesh.Application.Abstractions;

namespace Blindmesh.Infrastructure.Time;

/// <summary>Production clock. Always UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
