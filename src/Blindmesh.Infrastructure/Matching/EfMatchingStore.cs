using Blindmesh.Application.Matching;
using Blindmesh.Domain.Projects;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Matching;

/// <summary>
/// The persistence of the matching module. The module owns no table and adds
/// no migration: the store reads the rows of the task, of the project and of
/// the account, and it writes nothing. The read is the whole of the module,
/// so the list of the candidates changes as soon as a declaration, a condition
/// or a contribution changes.
/// </summary>
public sealed class EfMatchingStore(BlindmeshDbContext dbContext) : IMatchingStore
{
    public async Task<MatchingTask?> FindTaskAsync(
        Guid taskId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == taskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == task.ProjectId, cancellationToken);

        if (project is null)
        {
            return null;
        }

        return new MatchingTask(
            task.Id,
            task.ProjectId,
            task.Visibility,
            task.Status,
            project.Visibility,
            project.Status,
            await IsInsiderAsync(project, accountId, cancellationToken));
    }

    public async Task<IReadOnlyList<MatchingTask>> ListProjectTasksAsync(
        Guid projectId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == projectId, cancellationToken);

        if (project is null)
        {
            return [];
        }

        var tasks = await dbContext.Tasks
            .AsNoTracking()
            .Where(row =>
                row.ProjectId == projectId &&
                (row.Status == TaskState.Open || row.Status == TaskState.InProgress))
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken);

        var isInsider = await IsInsiderAsync(project, accountId, cancellationToken);

        return tasks
            .Select(task => new MatchingTask(
                task.Id,
                task.ProjectId,
                task.Visibility,
                task.Status,
                project.Visibility,
                project.Status,
                isInsider))
            .ToArray();
    }

    public async Task<IReadOnlyList<MatchingAccount>> ListAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => accountIds.Contains(user.Id))
            .Select(user => new MatchingAccount(
                user.Id,
                user.Username,
                user.DisplayName,
                user.IsPublic,
                user.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// True when the account is the owner of the project or a member of it.
    /// The insiders of a project read everything of it.
    /// </summary>
    private async Task<bool> IsInsiderAsync(
        Project project,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (project.OwnerId == accountId)
        {
            return true;
        }

        return await dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.ProjectId == project.Id && member.UserId == accountId,
                cancellationToken);
    }
}
