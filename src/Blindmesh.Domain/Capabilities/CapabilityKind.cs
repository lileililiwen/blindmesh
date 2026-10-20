namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// The kind of a capability. The kind says what the contributor offers, so a
/// hard condition of a task can be matched against it.
/// </summary>
public enum CapabilityKind
{
    /// <summary>An account of a service. The declaration names it, it never holds a credential.</summary>
    Account,

    /// <summary>A device the contributor works with.</summary>
    Device,

    /// <summary>A skill the contributor has.</summary>
    Skill,

    /// <summary>A region the contributor works in.</summary>
    Region,

    /// <summary>A network the contributor reaches, for example a laboratory network.</summary>
    Network,

    /// <summary>A role the contributor takes in a task.</summary>
    Role,
}
