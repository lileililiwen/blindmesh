using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The reader of the hard conditions of a task for the matching module. The
/// task package owns the conditions of a task; the capabilities module owns
/// the evaluation of them; the matching module owns the match. The reader
/// applies no scope rule, so the matching module never depends on the reader
/// that applies the scope and no seam creates a cycle at runtime.
/// </summary>
public sealed class TaskRequirementsReader(ITaskStore store) : ITaskRequirementsReader
{
    public async Task<IReadOnlyList<CapabilityHardCondition>?> ReadAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await store.FindTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        var requirements = await store.ListRequirementsAsync([taskId], cancellationToken);

        return requirements
            .GetValueOrDefault(taskId, [])
            .Select(requirement => new CapabilityHardCondition(
                TaskRequirementKinds.Map(requirement.Kind),
                requirement.Value))
            .ToArray();
    }
}
