namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// A capability of a contributor. The row holds the editable content, the
/// visibility, the lifecycle state, the verification state and the optimistic
/// concurrency version. A capability is never deleted; it is withdrawn and
/// keeps its activity.
/// </summary>
public sealed class Capability
{
    public Guid Id { get; set; }

    /// <summary>The owner of the capability. Only the owner may change it.</summary>
    public Guid OwnerId { get; set; }

    public CapabilityKind Kind { get; set; }

    /// <summary>The public label of the capability. It is the name a visitor reads.</summary>
    public string Label { get; set; } = string.Empty;

    public CapabilityVisibility Visibility { get; set; } = CapabilityVisibility.Private;

    /// <summary>False when the owner cannot offer the capability at the moment.</summary>
    public bool Available { get; set; } = true;

    public CapabilityStatus Status { get; set; } = CapabilityStatus.Declared;

    public CapabilityVerification Verification { get; set; } = CapabilityVerification.SelfDeclared;

    /// <summary>
    /// The non sensitive identifiers of the capability, for example the
    /// operating system or the browser. Only the keys of the allowlist of
    /// <see cref="CapabilityRules"/> are accepted.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>The optimistic concurrency version of the editable capability.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
