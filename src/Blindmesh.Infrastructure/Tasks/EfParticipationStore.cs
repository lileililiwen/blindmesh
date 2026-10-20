using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Projects;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Tasks;

/// <summary>
/// The persistence of the participations. The store is scoped, so it shares the
/// scoped database context and the transaction of the request with the escrow
/// primitives of the credit module. The lock of a participation comes after
/// the lock of the task and before the lock of the accounts, as the shared
/// contract of the lock order demands.
/// </summary>
public sealed class EfParticipationStore(BlindmeshDbContext dbContext) : IParticipationStore
{
    public Task<Participation?> FindParticipationAsync(Guid participationId, CancellationToken cancellationToken) =>
        dbContext.Participations.FirstOrDefaultAsync(
            participation => participation.Id == participationId,
            cancellationToken);

    public async Task<Participation?> LockParticipationAsync(
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A participation command must run inside the transaction of the caller.");

        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT id FROM participations WHERE id = @id FOR UPDATE";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = participationId;
        command.Parameters.Add(parameter);

        var locked = await command.ExecuteScalarAsync(cancellationToken);

        if (locked is null)
        {
            return null;
        }

        return await dbContext.Participations.FirstOrDefaultAsync(
            participation => participation.Id == participationId,
            cancellationToken);
    }

    public Task<TaskApplication?> FindApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken) =>
        dbContext.TaskApplications.FirstOrDefaultAsync(
            application => application.Id == applicationId,
            cancellationToken);

    public Task<Participation?> FindParticipationForContributorAsync(
        Guid taskId,
        Guid contributorId,
        CancellationToken cancellationToken) =>
        dbContext.Participations.FirstOrDefaultAsync(
            participation => participation.TaskId == taskId &&
                participation.ContributorId == contributorId,
            cancellationToken);

    public Task<bool> HasRunningParticipationAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Participations.AnyAsync(
            participation => participation.TaskId == taskId &&
                participation.State == ParticipationState.Running,
            cancellationToken);

    public Task<bool> HasReviewPendingParticipationAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Participations.AnyAsync(
            participation => participation.TaskId == taskId &&
                (participation.State == ParticipationState.Submitted ||
                    participation.State == ParticipationState.ChangesRequested ||
                    participation.State == ParticipationState.Disputed),
            cancellationToken);

    public Task AddParticipationAsync(Participation participation, CancellationToken cancellationToken) =>
        dbContext.Participations.AddAsync(participation, cancellationToken).AsTask();

    public Task AddApplicationAsync(TaskApplication application, CancellationToken cancellationToken) =>
        dbContext.TaskApplications.AddAsync(application, cancellationToken).AsTask();

    public Task AddLeaseAsync(SlotLease lease, CancellationToken cancellationToken) =>
        dbContext.SlotLeases.AddAsync(lease, cancellationToken).AsTask();

    public async Task ReleaseLeaseAsync(Guid leaseId, DateTimeOffset releasedAt, CancellationToken cancellationToken)
    {
        var lease = await dbContext.SlotLeases.FirstOrDefaultAsync(
            candidate => candidate.Id == leaseId,
            cancellationToken);

        if (lease is not null)
        {
            lease.ReleasedAt = releasedAt;
        }
    }

    public Task AddStatusChangeAsync(ParticipationStatusChange change, CancellationToken cancellationToken) =>
        dbContext.ParticipationStatusChanges.AddAsync(change, cancellationToken).AsTask();

    public async Task<IReadOnlyList<ParticipationStatusChange>> ListHistoryAsync(
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ParticipationStatusChanges
            .Where(change => change.ParticipationId == participationId)
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<ParticipationListRows> ListAsync(
        ParticipationListQuery query,
        Guid? contributorId,
        CancellationToken cancellationToken)
    {
        var rows = from participation in dbContext.Participations.AsNoTracking()
                   join task in dbContext.Tasks.AsNoTracking()
                       on participation.TaskId equals task.Id
                   join project in dbContext.Projects.AsNoTracking()
                       on task.ProjectId equals project.Id
                   select new
                   {
                       Participation = participation,
                       Task = task,
                       Project = project,
                   };

        if (contributorId is Guid contributor)
        {
            rows = rows.Where(row => row.Participation.ContributorId == contributor);
        }

        if (query.TaskId is Guid taskId)
        {
            rows = rows.Where(row => row.Participation.TaskId == taskId);
        }

        if (query.Cursor is ParticipationCursor cursor)
        {
            rows = rows.Where(row =>
                row.Participation.CreatedAt < cursor.CreatedAt ||
                (row.Participation.CreatedAt == cursor.CreatedAt && row.Participation.Id < cursor.Id));
        }

        var items = await rows
            .OrderByDescending(row => row.Participation.CreatedAt)
            .ThenByDescending(row => row.Participation.Id)
            .Take(query.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > query.Limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        ParticipationCursor? next = null;

        if (hasMore && items.Count > 0)
        {
            var last = items[^1].Participation;
            next = new ParticipationCursor(last.CreatedAt, last.Id);
        }

        var slotIds = items
            .Select(item => item.Participation.SlotId)
            .Where(slotId => slotId.HasValue)
            .Select(slotId => slotId!.Value)
            .Distinct()
            .ToArray();

        var slots = slotIds.Length == 0
            ? []
            : await dbContext.TaskSlots
                .Where(slot => slotIds.Contains(slot.Id))
                .ToDictionaryAsync(slot => slot.Id, slot => slot, cancellationToken);

        var mapped = items
            .Select(item =>
            {
                var slot = item.Participation.SlotId is Guid slotId && slots.TryGetValue(slotId, out var found)
                    ? found
                    : null;

                return new ParticipationListRow(
                    item.Participation.Id,
                    item.Participation.TaskId,
                    item.Participation.ContributorId,
                    item.Task.OwnerId,
                    item.Project.Id,
                    item.Task.Title,
                    item.Participation.State,
                    item.Participation.AppliedAt,
                    item.Participation.AcceptedAt,
                    item.Participation.StartedAt,
                    item.Participation.ExecutionDeadline,
                    item.Participation.CreatedAt,
                    slot?.Reward ?? 0,
                    slot?.State,
                    item.Participation.Version);
            })
            .ToArray();

        return new ParticipationListRows(mapped, next);
    }

    public async Task<IReadOnlyList<Participation>> ListOverdueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var decisionDeadline = now.AddHours(-ParticipationRules.ApplicationDecisionHours);
        var startDeadline = now.AddHours(-ParticipationRules.StartWindowHours);

        return await dbContext.Participations
            .AsNoTracking()
            .Where(participation =>
                (participation.State == ParticipationState.Applied &&
                 participation.AppliedAt <= decisionDeadline) ||
                (participation.State == ParticipationState.Accepted &&
                 participation.AcceptedAt != null &&
                 participation.AcceptedAt <= startDeadline))
            .OrderBy(participation => participation.AppliedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
