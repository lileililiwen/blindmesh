namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// A hard condition of a task. The condition names the kind of capability the
/// task needs and the value it has to match. A condition is matched against a
/// public and available declaration of the contributor; the label and the tags
/// of the declaration are the values that are compared.
/// </summary>
public sealed record CapabilityHardCondition(CapabilityKind Kind, string Value)
{
    /// <summary>
    /// The kinds a hard condition may use. The other kinds of a capability are
    /// offered by the contributor, but a task never requires them as a hard
    /// condition.
    /// </summary>
    public static IReadOnlyList<CapabilityKind> MatchableKinds { get; } =
    [
        CapabilityKind.Region,
        CapabilityKind.Device,
        CapabilityKind.Role,
    ];
}
