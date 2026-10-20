using System.Net;
using System.Text.Json;
using Blindmesh.Application.Credits;
using Blindmesh.Domain.Credits;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Credits;

/// <summary>
/// CRED-1 Conservation: the entries of a transaction sum to zero, the ordinary
/// bucket of a contributor never goes negative, a command that would overspend
/// or that reuses a key with another payload answers a conflict without a
/// partial entry, and a command that is retried with the same key and the same
/// payload returns the original answer without a second entry.
/// </summary>
[Collection(CreditCollection.Name)]
public sealed class Cred1ConservationTests : IAsyncLifetime
{
    private const string TaskReference = "task-credits-1";

    private readonly RuntimeFixture _fixture;

    public Cred1ConservationTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Every test starts with an empty ledger.</summary>
    public async Task InitializeAsync() => await CreditLedgerScope.ResetAsync(_fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cred1SuccessAReservationAndASettlementKeepTheJournalBalanced()
    {
        await using var ledger = new CreditLedgerScope(_fixture);
        var publisher = await ledger.CreateUserAsync(
            "publisher",
            "publisher@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred1-publisher");
        var contributor = await ledger.CreateUserAsync(
            "contributor",
            "contributor@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred1-contributor");

        var granted = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
            new CreditActor(publisher, "publisher"),
            new CreditGrantCommand(true, "cred1-success-grant-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, granted.Status);
        Assert.Equal(CreditRules.GrantAmount, granted.Wallet!.Available);
        Assert.Equal(0, granted.Wallet.Reserved);
        Assert.Equal(0, granted.Wallet.Pending);

        var reserved = await ledger.RunAsync(async () => await ledger.Service.ReserveTaskAsync(
            new CreditActor(publisher, "publisher"),
            new CreditReserveCommand(40, TaskReference, "cred1-success-reserve-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, reserved.Status);
        Assert.Equal(20, reserved.Wallet!.Available);
        Assert.Equal(40, reserved.Wallet.Reserved);
        Assert.Equal(0, reserved.Wallet.Pending);

        var pending = await ledger.RunAsync(async () => await ledger.Service.BeginPendingAsync(
            new CreditActor(publisher, "publisher"),
            new CreditPendingCommand(TaskReference, contributor, 40, "cred1-success-pending-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, pending.Status);

        var settled = await ledger.RunAsync(async () => await ledger.Service.SettleAsync(
            new CreditActor(contributor, "contributor"),
            new CreditSettleCommand(TaskReference, 40, "cred1-success-settle-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, settled.Status);
        Assert.Equal(40, settled.Wallet!.Available);
        Assert.Equal(0, settled.Wallet.Pending);

        // Every transaction of the journal sums to zero.
        var sums = await ledger.TransactionSumsAsync();

        Assert.Equal(4, sums.Count);
        Assert.All(sums, sum => Assert.Equal(0, sum.Sum));

        // The entries of the journal are never changed.
        var entries = await ledger.DbContext.LedgerEntries.AsNoTracking().ToListAsync();

        Assert.Equal(8, entries.Count);
        Assert.All(entries, entry => Assert.NotEqual(default, entry.Id));

        // The balances of the journal rebuild the balances of the wallet.
        Assert.Equal(20, await ledger.JournalBalanceAsync(publisher, LedgerBucket.UserAvailable));
        Assert.Equal(0, await ledger.JournalBalanceAsync(publisher, LedgerBucket.Reserved));
        Assert.Equal(0, await ledger.JournalBalanceAsync(publisher, LedgerBucket.Pending));
        Assert.Equal(40, await ledger.JournalBalanceAsync(contributor, LedgerBucket.UserAvailable));
        Assert.Equal(0, await ledger.JournalBalanceAsync(contributor, LedgerBucket.Pending));

        // The cached balances of the lots reconcile with the journal.
        var publisherLots = await ledger.LotsAsync(publisher);
        var contributorLots = await ledger.LotsAsync(contributor);

        Assert.Equal(20, publisherLots.Sum(lot => lot.AvailableAmount));
        Assert.Equal(0, publisherLots.Sum(lot => lot.ReservedAmount));
        Assert.Equal(0, publisherLots.Sum(lot => lot.PendingAmount));
        Assert.Equal(40, contributorLots.Sum(lot => lot.AvailableAmount));
        Assert.Equal(0, contributorLots.Sum(lot => lot.PendingAmount));

        // The supply of the platform matches the sum of the issued credits.
        Assert.Equal(-60, await ledger.PlatformBalanceAsync(LedgerBucket.PlatformIssuance));
        Assert.Equal(0, await ledger.PlatformBalanceAsync(LedgerBucket.PlatformExpiry));
    }

    [Fact]
    public async Task Cred1RejectionTheConcurrentOverspendCommitsOnceAndTheOtherAnswersAConflict()
    {
        await using var setup = new CreditLedgerScope(_fixture);
        var publisher = await setup.CreateUserAsync(
            "overspend",
            "overspend@blindmesh.test",
            setup.Clock.UtcNow.AddDays(-30),
            "cred1-overspend");

        var granted = await setup.RunAsync(async () => await setup.Service.GrantOnceAsync(
            new CreditActor(publisher, "overspend"),
            new CreditGrantCommand(true, "cred1-overspend-grant-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, granted.Status);

        // Two commands reserve the whole balance of the account at the same
        // moment. Only one of them may succeed.
        await using var first = new CreditLedgerScope(_fixture);
        await using var second = new CreditLedgerScope(_fixture);

        var firstResult = first.RunAsync(async () => await first.Service.ReserveTaskAsync(
            new CreditActor(publisher, "overspend"),
            new CreditReserveCommand(60, "task-overspend-a", "cred1-overspend-reserve-a"),
            CancellationToken.None));

        var secondResult = second.RunAsync(async () => await second.Service.ReserveTaskAsync(
            new CreditActor(publisher, "overspend"),
            new CreditReserveCommand(60, "task-overspend-b", "cred1-overspend-reserve-b"),
            CancellationToken.None));

        var results = await Task.WhenAll(firstResult, secondResult);

        Assert.Single(results, result => result.Status == CreditCommandStatus.Succeeded);
        Assert.Single(results, result => result.Status == CreditCommandStatus.Conflict);

        // The refused command left no partial entry behind.
        await using var check = new CreditLedgerScope(_fixture);

        Assert.Equal(0, await check.JournalBalanceAsync(publisher, LedgerBucket.UserAvailable));
        Assert.Equal(60, await check.JournalBalanceAsync(publisher, LedgerBucket.Reserved));
        Assert.Equal(0, await check.JournalBalanceAsync(publisher, LedgerBucket.Pending));

        var sums = await check.TransactionSumsAsync();

        Assert.Equal(2, sums.Count);
        Assert.All(sums, sum => Assert.Equal(0, sum.Sum));

        var lots = await check.LotsAsync(publisher);

        Assert.Equal(0, lots.Sum(lot => lot.AvailableAmount));
        Assert.Equal(60, lots.Sum(lot => lot.ReservedAmount));
    }

    [Fact]
    public async Task Cred1RejectionTheWalletOfAnotherContributorIsNotFoundAndHoldsNoAmount()
    {
        await using var ledger = new CreditLedgerScope(_fixture);
        var owner = await ledger.CreateUserAsync(
            "owner",
            "owner@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred1-owner");
        var other = await ledger.CreateUserAsync(
            "other",
            "other@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred1-other");

        await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
            new CreditActor(owner, "owner"),
            new CreditGrantCommand(true, "cred1-visibility-grant-0001"),
            CancellationToken.None));

        var wallet = await ledger.Service.GetWalletAsync(new CreditActor(owner, "owner"), owner, CancellationToken.None);

        Assert.NotNull(wallet);
        Assert.Equal(60, wallet.Available);

        // The wallet of another contributor is not found and carries no amount.
        var foreign = await ledger.Service.GetWalletAsync(new CreditActor(other, "other"), owner, CancellationToken.None);

        Assert.Null(foreign);

        // A visitor without a session reads nothing.
        var anonymous = await ledger.Service.GetWalletAsync(CreditActor.Anonymous, owner, CancellationToken.None);

        Assert.Null(anonymous);
    }

    [Fact]
    public async Task Cred1BoundaryTheRetriedKeyReturnsTheOriginalAnswerWithoutASecondEntry()
    {
        using var host = new CreditTestHost(_fixture);

        await host.SignInAsync("bob");

        const string Key = "cred1-boundary-retry-key-0001";
        const string Body = "{\"rules_accepted\":true}";

        var first = await host.SendGrantAsync(Key, Body);
        var second = await host.SendGrantAsync(Key, Body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstBody = await first.Content.ReadAsStringAsync(CancellationToken.None);
        var secondBody = await second.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(firstBody, secondBody);

        // The replay added no second entry.
        var state = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            Transactions = await dbContext.LedgerTransactions.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
            Lots = await dbContext.CreditLots.CountAsync(),
            Balance = await dbContext.LedgerEntries.SumAsync(entry => entry.Amount),
        });

        Assert.Equal(1, state.Claims);
        Assert.Equal(1, state.Transactions);
        Assert.Equal(2, state.Entries);
        Assert.Equal(1, state.Lots);
        Assert.Equal(0, state.Balance);

        // The same key with another payload is a conflict and issues nothing.
        var reused = await host.SendGrantAsync(Key, "{\"rules_accepted\":false}");

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        var afterReuse = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
        });

        Assert.Equal(1, afterReuse.Claims);
        Assert.Equal(2, afterReuse.Entries);
    }

    [Fact]
    public async Task Cred1BoundaryTheGrantCommandWithoutAKeyIsRefusedWithoutAnEntry()
    {
        using var host = new CreditTestHost(_fixture);

        await host.SignInAsync("bob");

        var refused = await host.SendGrantAsync(null, "{\"rules_accepted\":true}");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        var root = document.RootElement;

        Assert.Equal("invalid_idempotency_key", root.GetProperty("error").GetProperty("code").GetString());

        var state = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
        });

        Assert.Equal(0, state.Claims);
        Assert.Equal(0, state.Entries);
    }
}
