using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The seam that hands the hard conditions of a published task to the
/// capabilities module. The task package owns the conditions of a task; the
/// capabilities module owns the evaluation of them. The seam keeps both apart:
/// the evaluation never reads a task table and the task package never owns the
/// matching.
/// </summary>
public interface ITaskConditionReader
{
    /// <summary>
    /// The hard conditions of a published task that the actor may see. Null
    /// means that there is no such task for the actor, so no eligibility is
    /// evaluated. The reader never returns the conditions of a task that the
    /// actor may not see.
    /// </summary>
    Task<IReadOnlyList<CapabilityHardCondition>?> ReadAsync(
        Guid taskId,
        Guid actorId,
        CancellationToken cancellationToken);
}
