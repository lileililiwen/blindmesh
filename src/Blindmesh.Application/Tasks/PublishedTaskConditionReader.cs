using Blindmesh.Application.Capabilities;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The reader of the hard conditions of a published task. The tasks module
/// owns the conditions of a task; the capabilities module owns the evaluation
/// of them. The reader answers with the conditions of a published task that
/// the actor may see and with null for every other task, so a task that the
/// actor may not see is never found through the eligibility of the
/// marketplace. The rule of the scope of the matching module decides, so the
/// eligibility never names a condition of a task that the list of the
/// marketplace does not show.
/// </summary>
public sealed class PublishedTaskConditionReader(ITaskStore store, ITaskMatchSeam match)
    : ITaskConditionReader
{
    public async Task<IReadOnlyList<CapabilityHardCondition>?> ReadAsync(
        Guid taskId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var task = await store.FindTaskAsync(taskId, cancellationToken);

        if (task is null || task.Status is not (TaskState.Open or TaskState.InProgress))
        {
            return null;
        }

        if (!await match.MatchesAsync(taskId, actorId, cancellationToken))
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
