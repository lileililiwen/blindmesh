using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>The query of the list of capabilities. The limit is clamped by the caller.</summary>
public sealed record CapabilityListQuery(int Limit, CapabilityCursor? Cursor);

/// <summary>One page of capabilities and the position of the next page.</summary>
public sealed record CapabilityPage(IReadOnlyList<Capability> Items, CapabilityCursor? Next);

/// <summary>
/// The persistence of the capabilities module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly. The write methods stage the change and the caller commits the
/// staged changes with <see cref="SaveChangesAsync"/> inside its transaction.
/// </summary>
public interface ICapabilityStore
{
    Task<Capability?> FindCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CapabilityTag>> ListTagsAsync(
        IReadOnlyCollection<Guid> capabilityIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CapabilityStatusChange>> ListStatusHistoryAsync(
        IReadOnlyCollection<Guid> capabilityIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The declarations of the owner that may be used for a match: the public
    /// ones that are available and not withdrawn. A private declaration never
    /// reaches a match, so a reason never discloses it.
    /// </summary>
    Task<IReadOnlyList<MatchableCapability>> ListMatchableAsync(
        Guid ownerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The matchable declarations of every account: the public ones that are
    /// available and not withdrawn. The rule is the rule of
    /// <see cref="ListMatchableAsync"/>, without the owner.
    /// </summary>
    Task<IReadOnlyList<MatchableCapability>> ListAllMatchableAsync(
        CancellationToken cancellationToken);

    /// <summary>The usernames of the given users. A missing user is left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ListUsernamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The capabilities the actor may see in the directory: the declared
    /// public ones and the declared ones of the actor.
    /// </summary>
    Task<CapabilityPage> ListVisibleAsync(Guid? actorId, CapabilityListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The capabilities of the owner, including the withdrawn ones. They keep
    /// their row and their history.
    /// </summary>
    Task<CapabilityPage> ListOwnedAsync(Guid ownerId, CapabilityListQuery query, CancellationToken cancellationToken);

    Task AddCapabilityAsync(Capability capability, CancellationToken cancellationToken);

    Task AddTagAsync(CapabilityTag tag, CancellationToken cancellationToken);

    Task AddStatusChangeAsync(CapabilityStatusChange change, CancellationToken cancellationToken);

    /// <summary>Removes the tags of a capability. The caller replaced them.</summary>
    Task RemoveTagsAsync(Guid capabilityId, CancellationToken cancellationToken);

    /// <summary>
    /// Commits the staged changes. False means that another request changed
    /// the same capability first, so the caller has to report a version
    /// conflict.
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
