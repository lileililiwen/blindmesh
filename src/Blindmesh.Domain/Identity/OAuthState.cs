namespace Blindmesh.Domain.Identity;

/// <summary>
/// A one-use OAuth state. Only the hash of the state and of the pre-auth
/// cookie binding are stored, so a leaked row cannot be replayed.
/// </summary>
public sealed class OAuthState
{
    public Guid Id { get; set; }

    /// <summary>The hash of the random state value.</summary>
    public string StateHash { get; set; } = string.Empty;

    /// <summary>The hash of the pre-auth cookie value the state is bound to.</summary>
    public string BindingHash { get; set; } = string.Empty;

    /// <summary>The allowlisted relative path the callback returns to.</summary>
    public string ReturnPath { get; set; } = "/";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the state was used. A used state is never accepted again.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
