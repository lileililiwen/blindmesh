namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// Who may read a capability. The visibility is enforced on every read, list,
/// detail and command.
/// </summary>
public enum CapabilityVisibility
{
    /// <summary>Every visitor may read the capability.</summary>
    Public,

    /// <summary>Only the owner of the capability may read it.</summary>
    Private,
}
