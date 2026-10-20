namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The reader of the conditions of a published task that this package ships.
/// The task publication package is a later package, so the marketplace holds
/// no published task with hard conditions yet and the reader reports that no
/// condition is available. It never invents a condition and never invents a
/// task.
/// </summary>
public sealed class NoPublishedTaskConditionReader : ITaskConditionReader
{
    public Task<IReadOnlyList<Domain.Capabilities.CapabilityHardCondition>?> ReadAsync(
        Guid taskId,
        Guid actorId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Domain.Capabilities.CapabilityHardCondition>?>(null);
}
