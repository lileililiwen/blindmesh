namespace Blindmesh.Application.Identity;

/// <summary>
/// The standing of an account. The moderation package owns the standing; the
/// identity module only reads it, so that a banned account is refused before
/// any domain or monetary effect.
/// </summary>
public sealed record AccountStanding(bool IsBanned, DateTimeOffset? BannedUntil, string? Reason);

/// <summary>
/// Reads the standing of an account. The reader is the single seam of the ban:
/// the session guard asks it before it hands out the actor of a request.
/// </summary>
public interface IAccountStandingReader
{
    Task<AccountStanding> GetAsync(Guid userId, CancellationToken cancellationToken);
}
