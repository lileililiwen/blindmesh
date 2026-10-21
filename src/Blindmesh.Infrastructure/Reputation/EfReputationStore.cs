using Blindmesh.Application.Reputation;
using Blindmesh.Domain.Reputation;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Reputation;

/// <summary>
/// The persistence of the reputation module. The store is scoped, so it shares
/// the scoped database context and the transaction of the worker that applies
/// the event. The module writes no ledger row: it only records the outcome of
/// the contributions of a contributor.
/// </summary>
public sealed class EfReputationStore(BlindmeshDbContext dbContext) : IReputationStore
{
    public Task<ReputationEvent?> FindEventBySourceAsync(Guid sourceEventId, CancellationToken cancellationToken) =>
        dbContext.ReputationEvents.FirstOrDefaultAsync(
            reputationEvent => reputationEvent.SourceEventId == sourceEventId,
            cancellationToken);

    public Task AddEventAsync(ReputationEvent reputationEvent, CancellationToken cancellationToken) =>
        dbContext.ReputationEvents.AddAsync(reputationEvent, cancellationToken).AsTask();

    public async Task<IReadOnlyList<ReputationEvent>> ListEventsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ReputationEvents
            .AsNoTracking()
            .Where(reputationEvent => reputationEvent.UserId == userId)
            .OrderBy(reputationEvent => reputationEvent.OccurredAt)
            .ThenBy(reputationEvent => reputationEvent.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task<ReputationScore?> FindScoreAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.ReputationScores.FirstOrDefaultAsync(
            score => score.UserId == userId,
            cancellationToken);

    public Task AddScoreAsync(ReputationScore score, CancellationToken cancellationToken) =>
        dbContext.ReputationScores.AddAsync(score, cancellationToken).AsTask();

    public Task AddSnapshotAsync(ReputationSnapshot snapshot, CancellationToken cancellationToken) =>
        dbContext.ReputationSnapshots.AddAsync(snapshot, cancellationToken).AsTask();

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken) > 0;
}
