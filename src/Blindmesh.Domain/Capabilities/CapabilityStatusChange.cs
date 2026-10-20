namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// One applied transition of the lifecycle of a capability. The history keeps
/// the transitions of a withdrawn declaration, so the withdrawal stays
/// readable after the capability stopped matching conditions.
/// </summary>
public sealed class CapabilityStatusChange
{
    public Guid Id { get; set; }

    public Guid CapabilityId { get; set; }

    public CapabilityStatus FromStatus { get; set; }

    public CapabilityStatus ToStatus { get; set; }

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
