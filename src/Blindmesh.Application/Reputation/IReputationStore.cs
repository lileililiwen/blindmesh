using Blindmesh.Domain.Reputation;

namespace Blindmesh.Application.Reputation;

/// <summary>
/// The persistence of the reputation module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly. The store is the only place that writes a reputation row.
/// </summary>
public interface IReputationStore
{
    /// <summary>
    /// The event of an outbox message. Null when the message was never
    /// applied, so a message that arrives twice is never applied twice.
    /// </summary>
    Task<ReputationEvent?> FindEventBySourceAsync(Guid sourceEventId, CancellationToken cancellationToken);

    /// <summary>Writes one line of the trail of a contributor.</summary>
    Task AddEventAsync(ReputationEvent reputationEvent, CancellationToken cancellationToken);

    /// <summary>
    /// The lines of a contributor, the oldest first. The score of the
    /// contributor is the sum of the deltas of these lines.
    /// </summary>
    Task<IReadOnlyList<ReputationEvent>> ListEventsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The score of a contributor, when it was written.</summary>
    Task<ReputationScore?> FindScoreAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes the score of a contributor.</summary>
    Task AddScoreAsync(ReputationScore score, CancellationToken cancellationToken);

    /// <summary>Writes one snapshot of the score of a contributor.</summary>
    Task AddSnapshotAsync(ReputationSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>Saves the changed rows of the module.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
