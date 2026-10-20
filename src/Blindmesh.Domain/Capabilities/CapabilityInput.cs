namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// The editable content of a capability. The service merges a change into the
/// current content and validates the result, so there is one definition of a
/// valid capability.
/// </summary>
public sealed class CapabilityInput
{
    public CapabilityKind Kind { get; set; }

    public string Label { get; set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public CapabilityVisibility Visibility { get; set; } = CapabilityVisibility.Private;

    public bool Available { get; set; } = true;

    public IReadOnlyDictionary<string, string> Metadata { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
