using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The fields of a capability a caller may change. A field that the caller left
/// out keeps its current value.
/// </summary>
public sealed class CapabilityPatch
{
    public CapabilityKind? Kind { get; init; }

    public string? Label { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public CapabilityVisibility? Visibility { get; init; }

    public bool? Available { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
