namespace Blindmesh.Domain.Moderation;

/// <summary>
/// The ban of one account. The row holds the standing of the account, the
/// reason of the ban and the moderator that applied it. There is one row per
/// banned account, so a second ban of the same account overwrites the first
/// one and the trail of the module keeps every decision of its own.
/// </summary>
public sealed class AccountBan
{
    public Guid Id { get; set; }

    /// <summary>The account that is banned. It is unique.</summary>
    public Guid UserId { get; set; }

    /// <summary>The moment the ban of the account ends. Null means it never ends.</summary>
    public DateTimeOffset? BannedUntil { get; set; }

    /// <summary>The reason of the ban. Only moderation staff may read it.</summary>
    public string? BanReason { get; set; }

    /// <summary>The moderator that applied the ban.</summary>
    public Guid? BannedBy { get; set; }

    /// <summary>The moment the ban was applied.</summary>
    public DateTimeOffset BannedAt { get; set; }

    /// <summary>The optimistic concurrency version of the ban.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
