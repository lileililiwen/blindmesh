namespace Blindmesh.Domain.Identity;

/// <summary>
/// A server side session. The cookie carries a random opaque token; only its
/// hash is stored, so a stolen database cannot be replayed as a cookie.
/// </summary>
public sealed class UserSession
{
    public Guid Id { get; set; }

    /// <summary>Null for a session that is not linked to a user yet.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The hash of the opaque cookie token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The last activity. The session expires after the idle window.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>The absolute expiry of the session.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
