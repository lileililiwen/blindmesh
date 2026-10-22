using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The seam that hands the hard conditions of a task to the matching module.
/// The task package owns the conditions of a task, and the reader returns them
/// without a scope rule, so the matching module never reads a task through the
/// reader that applies the scope of a task. The seam keeps both apart: the
/// matching module never reads a task table and the task package never owns
/// the matching.
/// </summary>
public interface ITaskRequirementsReader
{
    /// <summary>
    /// The hard conditions of a task. Null means that there is no such task,
    /// so the caller has no condition to evaluate.
    /// </summary>
    Task<IReadOnlyList<CapabilityHardCondition>?> ReadAsync(
        Guid taskId,
        CancellationToken cancellationToken);
}
