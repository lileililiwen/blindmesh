namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// A tag of a capability. The tag is a short keyword of the declaration, for
/// example the model of a device. It is stored case folded, so a match against
/// a hard condition is deterministic.
/// </summary>
public sealed class CapabilityTag
{
    public Guid Id { get; set; }

    public Guid CapabilityId { get; set; }

    /// <summary>The case folded keyword.</summary>
    public string Tag { get; set; } = string.Empty;
}
