namespace Blindmesh.Domain.Identity;

/// <summary>
/// A person of the marketplace. The row holds the private identity fields
/// (provider login and email) next to the public profile fields. The public
/// read path never returns the private ones.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>The provider login of the linked identity. Never public.</summary>
    public string GithubLogin { get; set; } = string.Empty;

    /// <summary>The verified provider email. Never public.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The public handle. It is stored case folded.</summary>
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    /// <summary>An IANA time zone name, for example Europe/Berlin.</summary>
    public string Timezone { get; set; } = "UTC";

    public string[] Skills { get; set; } = [];

    public string[] Languages { get; set; } = [];

    public string Role { get; set; } = UserRole.User;

    /// <summary>A ban is applied by the moderation package and refused here.</summary>
    public bool IsBanned { get; set; }

    /// <summary>A private profile is not readable by a visitor.</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>The optimistic concurrency version of the editable profile.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
