using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// One row of the list of the participations. The row carries the seat with the
/// task, the project and the slot it belongs to, so the list never runs a
/// query per participation.
/// </summary>
public sealed record ParticipationListRow(
    Guid Id,
    Guid TaskId,
    Guid ContributorId,
    Guid OwnerId,
    Guid ProjectId,
    string TaskTitle,
    ParticipationState State,
    DateTimeOffset AppliedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ExecutionDeadline,
    DateTimeOffset CreatedAt,
    long SlotReward,
    TaskSlotState? SlotState,
    int Version);

/// <summary>One page of rows of the list of the participations.</summary>
public sealed record ParticipationListRows(
    IReadOnlyList<ParticipationListRow> Rows,
    ParticipationCursor? Next);

/// <summary>
/// The persistence of the participations. The store is scoped, so it shares the
/// scoped database context and the transaction of the request with the escrow
/// primitives of the credit module. The lock of a participation comes after
/// the lock of the task and before the lock of the accounts, as the shared
/// contract of the lock order demands.
/// </summary>
public interface IParticipationStore
{
    /// <summary>Reads one participation without a lock.</summary>
    Task<Participation?> FindParticipationAsync(Guid participationId, CancellationToken cancellationToken);

    /// <summary>Locks one participation for the transaction of the caller.</summary>
    Task<Participation?> LockParticipationAsync(Guid participationId, CancellationToken cancellationToken);

    /// <summary>Reads the application of a seat.</summary>
    Task<TaskApplication?> FindApplicationAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Reads the participation of one contributor in one task.</summary>
    Task<Participation?> FindParticipationForContributorAsync(
        Guid taskId,
        Guid contributorId,
        CancellationToken cancellationToken);

    /// <summary>True when at least one participation of the task is running.</summary>
    Task<bool> HasRunningParticipationAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// True when at least one participation of the task waits for the review of
    /// the owner or for the arbitration.
    /// </summary>
    Task<bool> HasReviewPendingParticipationAsync(Guid taskId, CancellationToken cancellationToken);

    Task AddParticipationAsync(Participation participation, CancellationToken cancellationToken);

    Task AddApplicationAsync(TaskApplication application, CancellationToken cancellationToken);

    Task AddLeaseAsync(SlotLease lease, CancellationToken cancellationToken);

    /// <summary>Releases the live lease of a slot.</summary>
    Task ReleaseLeaseAsync(Guid leaseId, DateTimeOffset releasedAt, CancellationToken cancellationToken);

    Task AddStatusChangeAsync(ParticipationStatusChange change, CancellationToken cancellationToken);

    /// <summary>The history of the lifecycle of one participation.</summary>
    Task<IReadOnlyList<ParticipationStatusChange>> ListHistoryAsync(
        Guid participationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The participations of one contributor, or the applications of one task
    /// when the task id is named.
    /// </summary>
    Task<ParticipationListRows> ListAsync(
        ParticipationListQuery query,
        Guid? contributorId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The participations that wait for a decision or for a start and that
    /// passed their deadline. The rows are read without a lock; the caller
    /// locks the task and the participation before it changes them.
    /// </summary>
    Task<IReadOnlyList<Participation>> ListOverdueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Writes the changes of the caller. False when another request changed the row first.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
