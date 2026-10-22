using Blindmesh.Application.Identity;
using Blindmesh.Domain.Identity;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Identity;

/// <summary>
/// The persistence of the identity module. Every write is a single statement
/// or a statement inside the command transaction of the caller, so a failed
/// command never leaves a partial sign in behind.
/// </summary>
public sealed class EfIdentityStore(BlindmeshDbContext dbContext) : IIdentityStore
{
    private const string InsertSavepoint = "identity_insert";

    public Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Username == username, cancellationToken);

    public Task<User?> FindUserByExternalIdentityAsync(string provider, string subject, CancellationToken cancellationToken) =>
        (from user in dbContext.Users
         join identity in dbContext.ExternalIdentities on user.Id equals identity.UserId
         where identity.Provider == provider && identity.Subject == subject
         select user).FirstOrDefaultAsync(cancellationToken);

    public Task<ExternalIdentity?> FindExternalIdentityAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.ExternalIdentities.FirstOrDefaultAsync(identity => identity.UserId == userId, cancellationToken);

    public async Task UpdateExternalAccountCreatedAtAsync(
        Guid userId,
        DateTimeOffset? accountCreatedAt,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.ExternalIdentities
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (identity is null || identity.AccountCreatedAt == accountCreatedAt)
        {
            return;
        }

        identity.AccountCreatedAt = accountCreatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAddUserAsync(User user, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction;

        if (transaction is not null)
        {
            await transaction.CreateSavepointAsync(InsertSavepoint, cancellationToken);
        }

        dbContext.Users.Add(user);
        dbContext.ExternalIdentities.Add(identity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUserConflict(exception))
        {
            // The unique username or the unique provider identity was taken.
            // The savepoint keeps the surrounding transaction usable.
            if (transaction is not null)
            {
                await transaction.RollbackToSavepointAsync(InsertSavepoint, cancellationToken);
            }

            dbContext.Entry(user).State = EntityState.Detached;
            dbContext.Entry(identity).State = EntityState.Detached;

            return false;
        }
    }

    public async Task<bool> TrySaveUserAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed the same profile first.
            return false;
        }
    }

    public Task<bool> IsUsernameTakenAsync(string username, Guid? otherThanUserId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user => user.Username == username && (otherThanUserId == null || user.Id != otherThanUserId),
            cancellationToken);

    public Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        dbContext.Sessions.FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

    public Task<UserSession?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Sessions.FirstOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        dbContext.Sessions.Update(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return;
        }

        dbContext.Sessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveExternalIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var identities = await dbContext.ExternalIdentities
            .Where(identity => identity.UserId == userId)
            .ToListAsync(cancellationToken);

        if (identities.Count == 0)
        {
            return;
        }

        dbContext.ExternalIdentities.RemoveRange(identities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeSessionsAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.Sessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return;
        }

        foreach (var session in sessions)
        {
            session.RevokedAt = revokedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OAuthState?> FindOAuthStateAsync(string stateHash, CancellationToken cancellationToken) =>
        dbContext.OAuthStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.StateHash == stateHash, cancellationToken);

    public async Task AddOAuthStateAsync(OAuthState state, CancellationToken cancellationToken)
    {
        dbContext.OAuthStates.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryConsumeOAuthStateAsync(Guid id, DateTimeOffset consumedAt, CancellationToken cancellationToken)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE oauth_states SET consumed_at = {consumedAt} WHERE id = {id} AND consumed_at IS NULL",
            cancellationToken);

        return affected == 1;
    }

    private static bool IsUserConflict(DbUpdateException exception) =>
        PostgresErrors.IsUniqueViolation(exception, "ux_users_username") ||
        PostgresErrors.IsUniqueViolation(exception, "ux_external_identities_provider_subject");
}
