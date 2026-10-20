using Blindmesh.Application.Credits;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Identity;
using Blindmesh.Infrastructure.Credits;
using Blindmesh.Infrastructure.Identity;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Credits;

/// <summary>
/// The credit ledger of a test. It holds the real database context, the real
/// store and the real service, so a test works on the committed state of the
/// database and not on a copy of it.
/// </summary>
internal sealed class CreditLedgerScope : IAsyncDisposable
{
    private readonly RuntimeFixture _fixture;

    public CreditLedgerScope(RuntimeFixture fixture, DateTimeOffset? now = null)
    {
        _fixture = fixture;
        Clock = new TestClock(now ?? DateTimeOffset.UtcNow);

        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        DbContext = new BlindmeshDbContext(options);
        Store = new EfCreditStore(DbContext);
        IdentityStore = new EfIdentityStore(DbContext);
        Service = new CreditService(Store, IdentityStore, Clock);
        Transactions = new EfCommandTransactionFactory(DbContext);
    }

    public TestClock Clock { get; }

    public BlindmeshDbContext DbContext { get; }

    public EfCreditStore Store { get; }

    public EfIdentityStore IdentityStore { get; }

    public CreditService Service { get; }

    public EfCommandTransactionFactory Transactions { get; }

    /// <summary>
    /// Runs a command in the transaction of the caller and commits it. The
    /// escrow primitives of the service expect such a transaction.
    /// </summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        await using var scope = await Transactions.BeginAsync(CancellationToken.None);

        try
        {
            var result = await action();
            await scope.CommitAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates a contributor with a verified provider email and an account
    /// that was created at the given moment.
    /// </summary>
    public async Task<Guid> CreateUserAsync(string prefix, string email, DateTimeOffset createdAt, string subject)
    {
        var username = $"{prefix}{Guid.NewGuid().ToString("N")[..8]}";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            GithubLogin = username,
            Email = email,
            Timezone = "UTC",
            Skills = [],
            Languages = [],
            Role = UserRole.User,
            IsPublic = true,
            Version = 1,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

        var identity = new ExternalIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = IdentityProviders.GitHub,
            Subject = subject,
            CreatedAt = createdAt,
        };

        var added = await IdentityStore.TryAddUserAsync(user, identity, CancellationToken.None);

        Assert.True(added);

        return user.Id;
    }

    /// <summary>The accounts of a contributor.</summary>
    public async Task<IReadOnlyList<LedgerAccount>> AccountsAsync(Guid ownerId) =>
        await DbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.OwnerId == ownerId)
            .ToListAsync()
            .ConfigureAwait(false);

    /// <summary>The signed balance of a bucket of a contributor, rebuilt from the journal.</summary>
    public async Task<long> JournalBalanceAsync(Guid ownerId, LedgerBucket bucket)
    {
        var accounts = await AccountsAsync(ownerId);
        var accountIds = accounts
            .Where(account => account.Bucket == bucket)
            .Select(account => account.Id)
            .ToArray();

        if (accountIds.Length == 0)
        {
            return 0;
        }

        return await DbContext.LedgerEntries
            .Where(entry => accountIds.Contains(entry.AccountId))
            .SumAsync(entry => entry.Amount);
    }

    /// <summary>The signed balance of a bucket of the platform, rebuilt from the journal.</summary>
    public async Task<long> PlatformBalanceAsync(LedgerBucket bucket)
    {
        var accounts = await DbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.OwnerId == null && account.Bucket == bucket)
            .ToListAsync();

        var accountIds = accounts.Select(account => account.Id).ToArray();

        if (accountIds.Length == 0)
        {
            return 0;
        }

        return await DbContext.LedgerEntries
            .Where(entry => accountIds.Contains(entry.AccountId))
            .SumAsync(entry => entry.Amount);
    }

    /// <summary>The lots of a contributor.</summary>
    public async Task<IReadOnlyList<CreditLot>> LotsAsync(Guid ownerId) =>
        await DbContext.CreditLots
            .AsNoTracking()
            .Where(lot => lot.UserId == ownerId)
            .ToListAsync();

    /// <summary>The signed sum of the entries of every transaction of the journal.</summary>
    public async Task<IReadOnlyList<(Guid TransactionId, long Sum)>> TransactionSumsAsync()
    {
        var entries = await DbContext.LedgerEntries.AsNoTracking().ToListAsync();

        return entries
            .GroupBy(entry => entry.TransactionId)
            .Select(group => (group.Key, group.Sum(entry => entry.Amount)))
            .ToList();
    }

    /// <summary>
    /// Empties the ledger of the test run. The limits of the grant are global,
    /// so every test starts with an empty journal, an empty set of claims and
    /// an empty set of idempotency records.
    /// </summary>
    public static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records");
    }

    public ValueTask DisposeAsync() => DbContext.DisposeAsync();
}
