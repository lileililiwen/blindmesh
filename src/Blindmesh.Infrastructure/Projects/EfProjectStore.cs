using Blindmesh.Application.Projects;
using Blindmesh.Domain.Projects;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Projects;

/// <summary>
/// The persistence of the projects module. Every write is staged inside the
/// command transaction of the caller and committed with the effects of the
/// command, so a refused command never leaves a partial state behind.
/// </summary>
public sealed class EfProjectStore(BlindmeshDbContext dbContext) : IProjectStore
{
    public async Task<Guid?> ResolveUserIdAsync(string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var value = reference.Trim();

        if (Guid.TryParse(value, out var id))
        {
            var known = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == id, cancellationToken);

            return known ? id : null;
        }

        // The username is stored case folded, so a different spelling finds the
        // same contributor.
        var username = value.ToLowerInvariant();

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Username == username)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken);

    public Task<ProjectMember?> FindMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.FirstOrDefaultAsync(
            member => member.ProjectId == projectId && member.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .OrderBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectVersion>> ListVersionsAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectVersions
            .AsNoTracking()
            .Where(version => version.ProjectId == projectId)
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectStatusChange>> ListStatusHistoryAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectStatusChanges
            .AsNoTracking()
            .Where(change => change.ProjectId == projectId)
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> ListUsernamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Username, cancellationToken);
    }

    public async Task<ProjectPage> ListVisibleAsync(
        Guid? actorId,
        ProjectListQuery query,
        CancellationToken cancellationToken)
    {
        var projects = dbContext.Projects.AsNoTracking();

        if (actorId is Guid userId)
        {
            projects = projects.Where(project =>
                project.Visibility == ProjectVisibility.Public ||
                project.OwnerId == userId ||
                dbContext.ProjectMembers.Any(member => member.ProjectId == project.Id && member.UserId == userId));
        }
        else
        {
            projects = projects.Where(project => project.Visibility == ProjectVisibility.Public);
        }

        if (query.Cursor is ProjectCursor cursor)
        {
            projects = projects.Where(project =>
                project.CreatedAt < cursor.CreatedAt ||
                (project.CreatedAt == cursor.CreatedAt && project.Id < cursor.Id));
        }

        var rows = await projects
            .OrderByDescending(project => project.CreatedAt)
            .ThenByDescending(project => project.Id)
            .Take(query.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        ProjectCursor? next = null;

        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            next = new ProjectCursor(last.CreatedAt, last.Id);
        }

        return new ProjectPage(rows, next);
    }

    public Task AddProjectAsync(Project project, CancellationToken cancellationToken)
    {
        dbContext.Projects.Add(project);

        return Task.CompletedTask;
    }

    public Task AddMemberAsync(ProjectMember member, CancellationToken cancellationToken)
    {
        dbContext.ProjectMembers.Add(member);

        return Task.CompletedTask;
    }

    public Task AddVersionAsync(ProjectVersion version, CancellationToken cancellationToken)
    {
        dbContext.ProjectVersions.Add(version);

        return Task.CompletedTask;
    }

    public Task AddStatusChangeAsync(ProjectStatusChange change, CancellationToken cancellationToken)
    {
        dbContext.ProjectStatusChanges.Add(change);

        return Task.CompletedTask;
    }

    public async Task<bool> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers.FirstOrDefaultAsync(
            item => item.ProjectId == projectId && item.UserId == userId,
            cancellationToken);

        if (member is null)
        {
            return false;
        }

        dbContext.ProjectMembers.Remove(member);

        return true;
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
            // Another request changed the same project first.
            return false;
        }
    }
}
