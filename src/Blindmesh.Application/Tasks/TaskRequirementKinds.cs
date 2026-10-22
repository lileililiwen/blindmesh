using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The kinds of a hard requirement of a task in the vocabulary of the
/// capabilities module. The mapping lives here, in the application assembly,
/// so the reader that applies the scope and the reader that hands the
/// conditions to the matching module never drift apart.
/// </summary>
internal static class TaskRequirementKinds
{
    /// <summary>
    /// The kind of a hard requirement. A requirement of an unknown kind is a
    /// region, because the domain never writes another kind.
    /// </summary>
    internal static CapabilityKind Map(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "device" => CapabilityKind.Device,
        "role" => CapabilityKind.Role,
        _ => CapabilityKind.Region,
    };
}
