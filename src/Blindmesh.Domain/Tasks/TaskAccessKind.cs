namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The kind of the access a task needs. A task that carries the access mode
/// names either a resource the owner operates or an official delegated consent.
/// A credential of a resource is never collected and never stored.
/// </summary>
public enum TaskAccessKind
{
    /// <summary>The resource is operated by the owner of the task.</summary>
    OwnerOperated,

    /// <summary>The owner of the resource delegated the access to the contributors.</summary>
    DelegatedConsent,
}
