using Blindmesh.Application.Capabilities;
using Blindmesh.Application.Projects;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The reader of the hard conditions of a published task. The tasks module
/// owns the conditions of a task; the capabilities module owns the evaluation
/// of them. The reader answers with the conditions of a published task that
/// the actor may see and with null for every other task, so a task that the
/// actor may not see is never found through the eligibility of the
/// marketplace.
/// </summary>
public sealed class PublishedTaskConditionReader(ITaskStore store, ProjectService projects)
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

        var access = await projects.GetAccessAsync(
            new ProjectActor(actorId, string.Empty),
            task.ProjectId,
            cancellationToken);

        if (access is null)
        {
            return null;
        }

        var visible = access.IsInsider ||
            (access.IsActive &&
             string.Equals(access.Visibility, "public", StringComparison.Ordinal) &&
             (task.Visibility == TaskVisibility.Public || task.Visibility == TaskVisibility.Matched));

        if (!visible)
        {
            return null;
        }

        var requirements = await store.ListRequirementsAsync([taskId], cancellationToken);

        return requirements
            .GetValueOrDefault(taskId, [])
            .Select(requirement => new CapabilityHardCondition(MapKind(requirement.Kind), requirement.Value))
            .ToArray();
    }

    /// <summary>
    /// The kind of a hard requirement in the vocabulary of the capabilities
    /// module. A requirement of an unknown kind is a region, because the
    /// domain never writes another kind.
    /// </summary>
    private static CapabilityKind MapKind(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "device" => CapabilityKind.Device,
        "role" => CapabilityKind.Role,
        _ => CapabilityKind.Region,
    };
}
