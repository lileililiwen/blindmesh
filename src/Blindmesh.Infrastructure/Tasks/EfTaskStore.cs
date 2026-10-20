using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Projects;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Tasks;

/// <summary>
/// The persistence of the tasks module. The store is scoped, so it shares the
/// scoped database context and the transaction of the request with the escrow
/// primitives of the credit module. The lock of a task comes before the lock
/// of the accounts, as the shared contract of the lock order demands.
/// </summary>
public sealed class EfTaskStore(BlindmeshDbContext dbContext) : ITaskStore
{
    public Task<MarketplaceTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Tasks.FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken);

    public async Task<MarketplaceTask?> LockTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A task command must run inside the transaction of the caller.");

        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT id FROM tasks WHERE id = @id FOR UPDATE";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = taskId;
        command.Parameters.Add(parameter);

        var locked = await command.ExecuteScalarAsync(cancellationToken);

        if (locked is null)
        {
            return null;
        }

        return await dbContext.Tasks.FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken);
    }

    public async Task<TaskSlot?> LockSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A task command must run inside the transaction of the caller.");

        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT id FROM task_slots WHERE id = @id FOR UPDATE";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = slotId;
        command.Parameters.Add(parameter);

        var locked = await command.ExecuteScalarAsync(cancellationToken);

        if (locked is null)
        {
            return null;
        }

        return await dbContext.TaskSlots.FirstOrDefaultAsync(slot => slot.Id == slotId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskRequirement>>> ListRequirementsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<TaskRequirement>>();
        }

        var rows = await dbContext.TaskRequirements
            .Where(requirement => taskIds.Contains(requirement.TaskId))
            .OrderBy(requirement => requirement.Kind)
            .ThenBy(requirement => requirement.Value)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(requirement => requirement.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TaskRequirement>)group.ToArray());
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskStep>>> ListStepsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<TaskStep>>();
        }

        var rows = await dbContext.TaskSteps
            .Where(step => taskIds.Contains(step.TaskId))
            .OrderBy(step => step.Position)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(step => step.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TaskStep>)group.ToArray());
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskSlot>>> ListSlotsAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<TaskSlot>>();
        }

        var rows = await dbContext.TaskSlots
            .Where(slot => taskIds.Contains(slot.TaskId))
            .OrderBy(slot => slot.Position)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(slot => slot.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TaskSlot>)group.ToArray());
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TaskStatusChange>>> ListStatusHistoryAsync(
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<TaskStatusChange>>();
        }

        var rows = await dbContext.TaskStatusChanges
            .Where(change => taskIds.Contains(change.TaskId))
            .OrderBy(change => change.ChangedAt)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(change => change.TaskId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TaskStatusChange>)group.ToArray());
    }

    public async Task<TaskPage> ListVisibleAsync(
        Guid? actorId,
        TaskListQuery query,
        CancellationToken cancellationToken)
    {
        var memberProjectIds = actorId is Guid memberId
            ? await dbContext.ProjectMembers
                .Where(member => member.UserId == memberId)
                .Select(member => member.ProjectId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        var rows = from task in dbContext.Tasks.AsNoTracking()
                   join project in dbContext.Projects on task.ProjectId equals project.Id
                   select new { Task = task, Project = project };

        if (actorId is Guid userId)
        {
            rows = rows.Where(row =>
                (row.Project.Visibility == ProjectVisibility.Public &&
                 row.Project.Status == ProjectStatus.Active &&
                 (row.Task.Status == TaskState.Open || row.Task.Status == TaskState.InProgress) &&
                 (row.Task.Visibility == TaskVisibility.Public ||
                  row.Task.Visibility == TaskVisibility.Matched)) ||
                row.Project.OwnerId == userId ||
                memberProjectIds.Contains(row.Project.Id));
        }
        else
        {
            rows = rows.Where(row =>
                row.Project.Visibility == ProjectVisibility.Public &&
                row.Project.Status == ProjectStatus.Active &&
                (row.Task.Status == TaskState.Open || row.Task.Status == TaskState.InProgress) &&
                (row.Task.Visibility == TaskVisibility.Public ||
                 row.Task.Visibility == TaskVisibility.Matched));
        }

        if (query.ProjectId is Guid projectId)
        {
            rows = rows.Where(row => row.Task.ProjectId == projectId);
        }

        if (query.Mode is TaskMode mode)
        {
            var modeValue = (int)mode;
            rows = rows.Where(row => ((int)row.Task.Modes & modeValue) != 0);
        }

        if (query.RequirementKind is string kind && query.RequirementValue is string value)
        {
            rows = rows.Where(row => dbContext.TaskRequirements.Any(requirement =>
                requirement.TaskId == row.Task.Id &&
                requirement.Kind == kind &&
                requirement.Value == value));
        }
        else if (query.RequirementKind is string kindOnly)
        {
            rows = rows.Where(row => dbContext.TaskRequirements.Any(requirement =>
                requirement.TaskId == row.Task.Id && requirement.Kind == kindOnly));
        }
        else if (query.RequirementValue is string valueOnly)
        {
            rows = rows.Where(row => dbContext.TaskRequirements.Any(requirement =>
                requirement.TaskId == row.Task.Id && requirement.Value == valueOnly));
        }

        if (query.Cursor is TaskCursor cursor)
        {
            rows = rows.Where(row =>
                row.Task.CreatedAt < cursor.CreatedAt ||
                (row.Task.CreatedAt == cursor.CreatedAt && row.Task.Id < cursor.Id));
        }

        var items = await rows
            .OrderByDescending(row => row.Task.CreatedAt)
            .ThenByDescending(row => row.Task.Id)
            .Take(query.Limit + 1)
            .Select(row => row.Task)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > query.Limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        TaskCursor? next = null;

        if (hasMore && items.Count > 0)
        {
            var last = items[^1];
            next = new TaskCursor(last.CreatedAt, last.Id);
        }

        return new TaskPage(items, next);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListUsernamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await dbContext.Users
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Username })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, row => row.Username);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListProjectNamesAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await dbContext.Projects
            .Where(project => projectIds.Contains(project.Id))
            .Select(project => new { project.Id, project.Name })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, row => row.Name);
    }

    public async Task AddTaskAsync(MarketplaceTask task, CancellationToken cancellationToken) =>
        await dbContext.Tasks.AddAsync(task, cancellationToken);

    public async Task AddRequirementsAsync(
        IReadOnlyList<TaskRequirement> requirements,
        CancellationToken cancellationToken) =>
        await dbContext.TaskRequirements.AddRangeAsync(requirements, cancellationToken);

    public async Task AddStepsAsync(IReadOnlyList<TaskStep> steps, CancellationToken cancellationToken) =>
        await dbContext.TaskSteps.AddRangeAsync(steps, cancellationToken);

    public async Task AddSlotsAsync(IReadOnlyList<TaskSlot> slots, CancellationToken cancellationToken) =>
        await dbContext.TaskSlots.AddRangeAsync(slots, cancellationToken);

    public async Task AddStatusChangeAsync(TaskStatusChange change, CancellationToken cancellationToken) =>
        await dbContext.TaskStatusChanges.AddAsync(change, cancellationToken);

    public async Task ReplaceRequirementsAsync(
        Guid taskId,
        IReadOnlyList<TaskRequirement> requirements,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TaskRequirements
            .Where(requirement => requirement.TaskId == taskId)
            .ToListAsync(cancellationToken);

        dbContext.TaskRequirements.RemoveRange(existing);
        await dbContext.TaskRequirements.AddRangeAsync(requirements, cancellationToken);
    }

    public async Task ReplaceStepsAsync(
        Guid taskId,
        IReadOnlyList<TaskStep> steps,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TaskSteps
            .Where(step => step.TaskId == taskId)
            .ToListAsync(cancellationToken);

        dbContext.TaskSteps.RemoveRange(existing);
        await dbContext.TaskSteps.AddRangeAsync(steps, cancellationToken);
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
