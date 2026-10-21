using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Findings;

/// <summary>
/// The locked seat of a submission. It carries the four rows of the seat, so
/// the moderation module never locks a row of the findings module on its own.
/// </summary>
public sealed record DisputeSeatLock(
    Submission Submission,
    MarketplaceTask Task,
    Participation Participation,
    TaskSlot? Slot);

/// <summary>
/// The seam of the findings module for the moderation module. It owns the
/// states of a seat: the findings module flips them, the moderation module
/// decides when. The seam never opens a transaction of its own, so the caller
/// and the seam share the transaction and the database context of the request.
/// </summary>
public interface IFindingsDisputeSeam
{
    /// <summary>
    /// Locks the seat of a submission in the order of the shared lock contract:
    /// the task, then the participation, then the slot, then the submission.
    /// Null means that the submission does not exist.
    /// </summary>
    Task<DisputeSeatLock?> LockSeatAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Flips the seat of a submission into the disputed state and writes its
    /// history. The settlement of the seat is frozen from this moment on.
    /// </summary>
    Task MarkDisputedAsync(
        DisputeSeatLock seat,
        string reason,
        Guid changedBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the seat of a submission after the arbitration. The lease of the
    /// slot is released and the slot is free again, so the task may take
    /// another contributor.
    /// </summary>
    Task EndDisputeAsync(
        DisputeSeatLock seat,
        bool accepted,
        Guid changedBy,
        CancellationToken cancellationToken);
}
