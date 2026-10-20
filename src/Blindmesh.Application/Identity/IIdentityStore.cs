using Blindmesh.Domain.Identity;

namespace Blindmesh.Application.Identity;

/// <summary>
/// The persistence of the identity module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly.
/// </summary>
public interface IIdentityStore
{
    Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<User?> FindUserByExternalIdentityAsync(string provider, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new user with its provider identity. False means that the
    /// username or the provider identity is already taken.
    /// </summary>
    Task<bool> TryAddUserAsync(User user, ExternalIdentity identity, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the changed user. False means that another request changed the
    /// same profile first, so the caller has to report a version conflict.
    /// </summary>
    Task<bool> TrySaveUserAsync(User user, CancellationToken cancellationToken);

    /// <summary>True when another user already owns the username.</summary>
    Task<bool> IsUsernameTakenAsync(string username, Guid? otherThanUserId, CancellationToken cancellationToken);

    Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<UserSession?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task RemoveSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<OAuthState?> FindOAuthStateAsync(string stateHash, CancellationToken cancellationToken);

    Task AddOAuthStateAsync(OAuthState state, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the state as used. False means that another request used it
    /// first, so the caller must not continue.
    /// </summary>
    Task<bool> TryConsumeOAuthStateAsync(Guid id, DateTimeOffset consumedAt, CancellationToken cancellationToken);
}
