using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Identity;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Moderation;

/// <summary>
/// Reads the standing of an account. The standing is owned by the moderation
/// package: the identity module only asks for it, so that a banned account is
/// refused before it changes anything. An account without a ban is never
/// banned.
/// </summary>
public sealed class EfAccountStandingReader(BlindmeshDbContext dbContext, IClock clock)
    : IAccountStandingReader
{
    public async Task<AccountStanding> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var ban = await dbContext.AccountBans
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        if (ban is null)
        {
            return new AccountStanding(false, null, null);
        }

        var now = clock.UtcNow;
        var banned = ban.BannedUntil is null || ban.BannedUntil > now;

        return new AccountStanding(banned, ban.BannedUntil, ban.BanReason);
    }
}
