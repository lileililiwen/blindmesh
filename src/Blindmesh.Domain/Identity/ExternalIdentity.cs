namespace Blindmesh.Domain.Identity;

/// <summary>
/// A provider identity of a user. The provider and the subject are unique, so
/// one provider account can only ever belong to one user.
/// </summary>
public sealed class ExternalIdentity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The provider name, for example github.</summary>
    public string Provider { get; set; } = IdentityProviders.GitHub;

    /// <summary>The stable subject of the account at the provider.</summary>
    public string Subject { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
