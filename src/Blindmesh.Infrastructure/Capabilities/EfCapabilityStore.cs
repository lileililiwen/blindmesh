using Blindmesh.Application.Capabilities;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Capabilities;

/// <summary>
/// The persistence of the capabilities module. Every write is staged inside the
/// command transaction of the caller and committed with the effects of the
/// command, so a refused command never leaves a partial state behind.
/// </summary>
public sealed class EfCapabilityStore(BlindmeshDbContext dbContext) : ICapabilityStore
{
    public Task<Capability?> FindCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken) =>
        dbContext.Capabilities.FirstOrDefaultAsync(capability => capability.Id == capabilityId, cancellationToken);

    public async Task<IReadOnlyList<CapabilityTag>> ListTagsAsync(
        IReadOnlyCollection<Guid> capabilityIds,
        CancellationToken cancellationToken)
    {
        if (capabilityIds.Count == 0)
        {
            return [];
        }

        return await dbContext.CapabilityTags
            .AsNoTracking()
            .Where(tag => capabilityIds.Contains(tag.CapabilityId))
            .OrderBy(tag => tag.Tag)
            .ThenBy(tag => tag.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CapabilityStatusChange>> ListStatusHistoryAsync(
        IReadOnlyCollection<Guid> capabilityIds,
        CancellationToken cancellationToken)
    {
        if (capabilityIds.Count == 0)
        {
            return [];
        }

        return await dbContext.CapabilityStatusChanges
            .AsNoTracking()
            .Where(change => capabilityIds.Contains(change.CapabilityId))
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);
    }

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

    public async Task<IReadOnlyList<MatchableCapability>> ListMatchableAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var capabilities = await dbContext.Capabilities
            .AsNoTracking()
            .Where(capability =>
                capability.OwnerId == ownerId &&
                capability.Visibility == CapabilityVisibility.Public &&
                capability.Available &&
                capability.Status == CapabilityStatus.Declared)
            .OrderBy(capability => capability.CreatedAt)
            .ThenBy(capability => capability.Id)
            .ToListAsync(cancellationToken);

        return await ToMatchableAsync(capabilities, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchableCapability>> ListAllMatchableAsync(
        CancellationToken cancellationToken)
    {
        var capabilities = await dbContext.Capabilities
            .AsNoTracking()
            .Where(capability =>
                capability.Visibility == CapabilityVisibility.Public &&
                capability.Available &&
                capability.Status == CapabilityStatus.Declared)
            .OrderBy(capability => capability.CreatedAt)
            .ThenBy(capability => capability.Id)
            .ToListAsync(cancellationToken);

        return await ToMatchableAsync(capabilities, cancellationToken);
    }

    /// <summary>
    /// The declarations with their tags. The rule of a matchable declaration is
    /// the same for one owner and for every owner: the declaration is public,
    /// it is available and it is not withdrawn.
    /// </summary>
    private async Task<IReadOnlyList<MatchableCapability>> ToMatchableAsync(
        IReadOnlyList<Capability> capabilities,
        CancellationToken cancellationToken)
    {
        if (capabilities.Count == 0)
        {
            return [];
        }

        var tags = await ListTagsAsync(
            capabilities.Select(capability => capability.Id).ToArray(),
            cancellationToken);

        return capabilities
            .Select(capability => new MatchableCapability(
                capability,
                tags
                    .Where(tag => tag.CapabilityId == capability.Id)
                    .Select(tag => tag.Tag)
                    .ToArray()))
            .ToArray();
    }

    public async Task<CapabilityPage> ListVisibleAsync(
        Guid? actorId,
        CapabilityListQuery query,
        CancellationToken cancellationToken)
    {
        var capabilities = dbContext.Capabilities
            .AsNoTracking()
            .Where(capability => capability.Status == CapabilityStatus.Declared);

        capabilities = actorId is Guid userId
            ? capabilities.Where(capability =>
                capability.Visibility == CapabilityVisibility.Public || capability.OwnerId == userId)
            : capabilities.Where(capability => capability.Visibility == CapabilityVisibility.Public);

        return await PageAsync(capabilities, query, cancellationToken);
    }

    public async Task<CapabilityPage> ListOwnedAsync(
        Guid ownerId,
        CapabilityListQuery query,
        CancellationToken cancellationToken)
    {
        var capabilities = dbContext.Capabilities
            .AsNoTracking()
            .Where(capability => capability.OwnerId == ownerId);

        return await PageAsync(capabilities, query, cancellationToken);
    }

    public Task AddCapabilityAsync(Capability capability, CancellationToken cancellationToken)
    {
        dbContext.Capabilities.Add(capability);

        return Task.CompletedTask;
    }

    public Task AddTagAsync(CapabilityTag tag, CancellationToken cancellationToken)
    {
        dbContext.CapabilityTags.Add(tag);

        return Task.CompletedTask;
    }

    public Task AddStatusChangeAsync(CapabilityStatusChange change, CancellationToken cancellationToken)
    {
        dbContext.CapabilityStatusChanges.Add(change);

        return Task.CompletedTask;
    }

    public async Task RemoveTagsAsync(Guid capabilityId, CancellationToken cancellationToken)
    {
        var tags = await dbContext.CapabilityTags
            .Where(tag => tag.CapabilityId == capabilityId)
            .ToListAsync(cancellationToken);

        dbContext.CapabilityTags.RemoveRange(tags);
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
            // Another request changed the same capability first.
            return false;
        }
    }

    private static async Task<CapabilityPage> PageAsync(
        IQueryable<Capability> capabilities,
        CapabilityListQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Cursor is CapabilityCursor cursor)
        {
            capabilities = capabilities.Where(capability =>
                capability.CreatedAt < cursor.CreatedAt ||
                (capability.CreatedAt == cursor.CreatedAt && capability.Id < cursor.Id));
        }

        var rows = await capabilities
            .OrderByDescending(capability => capability.CreatedAt)
            .ThenByDescending(capability => capability.Id)
            .Take(query.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        CapabilityCursor? next = null;

        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            next = new CapabilityCursor(last.CreatedAt, last.Id);
        }

        return new CapabilityPage(rows, next);
    }
}
