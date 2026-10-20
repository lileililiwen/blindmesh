using System.Net;
using System.Text.Json;
using Blindmesh.Application.Credits;
using Blindmesh.Domain.Credits;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Credits;

/// <summary>
/// CRED-2 Grant: the grant is issued once per provider subject, it holds
/// exactly 60 credits, it expires after 30 days, it obeys the daily cap of
/// the platform and the daily limit of an address, and a grant that runs out
/// while it waits in a reservation is paid as an earned credit or goes to the
/// expiry sink.
/// </summary>
[Collection(CreditCollection.Name)]
public sealed class Cred2GrantTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    public Cred2GrantTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Every test starts with an empty ledger.</summary>
    public async Task InitializeAsync() => await CreditLedgerScope.ResetAsync(_fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cred2SuccessAnEligibleAccountClaimsExactlySixtyCreditsWithTheExpiry()
    {
        using var host = new CreditTestHost(_fixture);

        await host.SignInAsync("bob");

        // The wallet of the contributor explains that the grant is available.
        var before = await host.GetWalletAsync();

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var beforeDocument = await host.ReadJsonAsync(before))
        {
            var grant = beforeDocument.RootElement.GetProperty("grant");

            Assert.True(grant.GetProperty("eligible").GetBoolean());
            Assert.Equal(60, grant.GetProperty("amount").GetInt64());
            Assert.Equal(30, grant.GetProperty("expires_in_days").GetInt32());
            Assert.Equal(7, grant.GetProperty("min_account_age_days").GetInt32());
            Assert.Equal(0, grant.GetProperty("reasons").GetArrayLength());
        }

        var claimed = await host.SendGrantAsync("cred2-success-claim-key-0001", "{\"rules_accepted\":true}");

        Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);

        using var claimedDocument = await host.ReadJsonAsync(claimed);
        var wallet = claimedDocument.RootElement;

        Assert.Equal(60, wallet.GetProperty("available").GetInt64());
        Assert.Equal(0, wallet.GetProperty("reserved").GetInt64());
        Assert.Equal(0, wallet.GetProperty("pending").GetInt64());

        var lot = Assert.Single(wallet.GetProperty("lots").EnumerateArray().ToArray());

        Assert.Equal("grant", lot.GetProperty("kind").GetString());
        Assert.Equal(60, lot.GetProperty("original_amount").GetInt64());
        Assert.Equal(60, lot.GetProperty("available_amount").GetInt64());
        Assert.False(lot.GetProperty("expired").GetBoolean());

        // The grant expires 30 days after the claim.
        var expiresAt = lot.GetProperty("expires_at").GetDateTimeOffset();
        var claimedAt = wallet.GetProperty("as_of").GetDateTimeOffset();

        Assert.Equal(30, Math.Round((expiresAt - claimedAt).TotalDays));

        // The committed rows are in the database and the journal is balanced.
        var state = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            ClaimAmount = await dbContext.GrantClaims.Select(claim => claim.Amount).FirstAsync(),
            ClaimExpiresAt = await dbContext.GrantClaims.Select(claim => claim.ExpiresAt).FirstAsync(),
            Transactions = await dbContext.LedgerTransactions.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
            Sum = await dbContext.LedgerEntries.SumAsync(entry => entry.Amount),
            Lots = await dbContext.CreditLots.CountAsync(),
        });

        Assert.Equal(1, state.Claims);
        Assert.Equal(60, state.ClaimAmount);
        Assert.Equal(1, state.Transactions);
        Assert.Equal(2, state.Entries);
        Assert.Equal(0, state.Sum);
        Assert.Equal(1, state.Lots);
        Assert.True(state.ClaimExpiresAt > claimedAt);
    }

    [Fact]
    public async Task Cred2RejectionTheSecondClaimOfTheSameSubjectIssuesNothing()
    {
        using var host = new CreditTestHost(_fixture);

        await host.SignInAsync("bob");

        var first = await host.SendGrantAsync("cred2-reuse-claim-key-0001", "{\"rules_accepted\":true}");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await host.SendGrantAsync("cred2-reuse-claim-key-0002", "{\"rules_accepted\":true}");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var document = await host.ReadJsonAsync(second);
        var root = document.RootElement;

        Assert.Equal("grant_already_claimed", root.GetProperty("error").GetProperty("code").GetString());

        // The wallet explains the condition.
        var wallet = await host.GetWalletAsync();

        using var walletDocument = await host.ReadJsonAsync(wallet);
        var grant = walletDocument.RootElement.GetProperty("grant");

        Assert.False(grant.GetProperty("eligible").GetBoolean());
        Assert.Contains(
            grant.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()),
            reason => reason is not null && reason.Contains("already claimed", StringComparison.OrdinalIgnoreCase));

        // The second claim issued nothing.
        var state = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            Transactions = await dbContext.LedgerTransactions.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
        });

        Assert.Equal(1, state.Claims);
        Assert.Equal(1, state.Transactions);
        Assert.Equal(2, state.Entries);
    }

    [Fact]
    public async Task Cred2RejectionTheAccountWithoutAVerifiedEmailOrTooYoungIsRefusedWithTheReason()
    {
        using var host = new CreditTestHost(_fixture);

        // The account of cara is one day old and has no verified email.
        await host.SignInAsync("cara");

        var refused = await host.SendGrantAsync("cred2-ineligible-claim-key-0001", "{\"rules_accepted\":true}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        var root = document.RootElement;

        Assert.Equal("grant_not_eligible", root.GetProperty("error").GetProperty("code").GetString());

        // The wallet explains every condition that is not met.
        var wallet = await host.GetWalletAsync();

        Assert.Equal(HttpStatusCode.OK, wallet.StatusCode);

        using var walletDocument = await host.ReadJsonAsync(wallet);
        var grant = walletDocument.RootElement.GetProperty("grant");

        Assert.False(grant.GetProperty("eligible").GetBoolean());

        var reasons = grant.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString() ?? string.Empty).ToArray();

        Assert.Contains(reasons, reason => reason.Contains("verified email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(reasons, reason => reason.Contains("days old", StringComparison.OrdinalIgnoreCase));

        // No issuance happened.
        var state = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
        });

        Assert.Equal(0, state.Claims);
        Assert.Equal(0, state.Entries);
    }

    [Fact]
    public async Task Cred2RejectionTheGrantWithoutTheAcceptedRulesIsRefused()
    {
        using var host = new CreditTestHost(_fixture);

        await host.SignInAsync("bob");

        var refused = await host.SendGrantAsync("cred2-rules-claim-key-0001", "{\"rules_accepted\":false}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        var root = document.RootElement;

        Assert.Equal("unprocessable_entity", root.GetProperty("error").GetProperty("code").GetString());
        Assert.True(root.GetProperty("error").GetProperty("field_errors").TryGetProperty("rules_accepted", out _));

        var state = await host.WithDatabaseAsync(async dbContext => new
        {
            Claims = await dbContext.GrantClaims.CountAsync(),
            Entries = await dbContext.LedgerEntries.CountAsync(),
        });

        Assert.Equal(0, state.Claims);
        Assert.Equal(0, state.Entries);
    }

    [Fact]
    public async Task Cred2RejectionTheDailyCapOfThePlatformHolds()
    {
        await using var ledger = new CreditLedgerScope(_fixture);

        // A hundred contributors claim the grant, one address each. The cap of
        // the platform is 6000 credits a day and every grant holds 60 credits,
        // so the hundredth claim uses the cap of the day up.
        for (var index = 0; index < 100; index++)
        {
            var userId = await ledger.CreateUserAsync(
                "cap",
                $"cap{index}@blindmesh.test",
                ledger.Clock.UtcNow.AddDays(-30),
                $"cred2-cap-{index}");

            var granted = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
                new CreditActor(userId, $"cap{index}"),
                new CreditGrantCommand(true, $"cred2-cap-claim-key-{index:D4}", $"10.0.{index / 250}.{index % 250 + 1}"),
                CancellationToken.None));

            Assert.Equal(CreditCommandStatus.Succeeded, granted.Status);
        }

        // The next claim is refused and issues nothing.
        var last = await ledger.CreateUserAsync(
            "cap",
            "cap-last@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred2-cap-last");

        var refused = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
            new CreditActor(last, "cap-last"),
            new CreditGrantCommand(true, "cred2-cap-claim-key-last", "10.9.9.9"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.LimitReached, refused.Status);
        Assert.Contains(CreditReasons.DailyCapReached, refused.Reasons ?? []);

        var claims = await ledger.DbContext.GrantClaims.CountAsync();
        var issued = await ledger.DbContext.GrantClaims.SumAsync(claim => claim.Amount);

        Assert.Equal(100, claims);
        Assert.Equal(CreditRules.GrantDailyGlobalCap, issued);
    }

    [Fact]
    public async Task Cred2RejectionTheDailyLimitOfAnAddressHolds()
    {
        await using var ledger = new CreditLedgerScope(_fixture);
        const string Address = "203.0.113.7";

        for (var index = 0; index < CreditRules.GrantDailyAddressLimit; index++)
        {
            var userId = await ledger.CreateUserAsync(
                "address",
                $"address{index}@blindmesh.test",
                ledger.Clock.UtcNow.AddDays(-30),
                $"cred2-address-{index}");

            var granted = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
                new CreditActor(userId, $"address{index}"),
                new CreditGrantCommand(true, $"cred2-address-claim-key-{index:D4}", Address),
                CancellationToken.None));

            Assert.Equal(CreditCommandStatus.Succeeded, granted.Status);
        }

        var refusedUser = await ledger.CreateUserAsync(
            "address",
            "address-last@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred2-address-last");

        var refused = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
            new CreditActor(refusedUser, "address-last"),
            new CreditGrantCommand(true, "cred2-address-claim-key-last", Address),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.LimitReached, refused.Status);
        Assert.Contains(CreditReasons.AddressLimitReached, refused.Reasons ?? []);

        var claims = await ledger.DbContext.GrantClaims.CountAsync();

        Assert.Equal(CreditRules.GrantDailyAddressLimit, claims);
    }

    [Fact]
    public async Task Cred2BoundaryAnExpiredReservedGrantIsPaidAsAnEarnedCreditThatNeverExpires()
    {
        await using var ledger = new CreditLedgerScope(_fixture);
        var publisher = await ledger.CreateUserAsync(
            "expiry",
            "expiry@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred2-expiry-publisher");
        var contributor = await ledger.CreateUserAsync(
            "expiry",
            "expiry-contributor@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred2-expiry-contributor");

        var granted = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
            new CreditActor(publisher, "expiry"),
            new CreditGrantCommand(true, "cred2-boundary-grant-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, granted.Status);

        var reserved = await ledger.RunAsync(async () => await ledger.Service.ReserveTaskAsync(
            new CreditActor(publisher, "expiry"),
            new CreditReserveCommand(60, "task-expiry-1", "cred2-boundary-reserve-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, reserved.Status);

        // The grant of the publisher runs out while it waits in the reservation.
        ledger.Clock.Advance(TimeSpan.FromDays(CreditRules.GrantExpiryDays + 1));

        var pending = await ledger.RunAsync(async () => await ledger.Service.BeginPendingAsync(
            new CreditActor(publisher, "expiry"),
            new CreditPendingCommand("task-expiry-1", contributor, 60, "cred2-boundary-pending-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, pending.Status);

        var settled = await ledger.RunAsync(async () => await ledger.Service.SettleAsync(
            new CreditActor(contributor, "expiry-contributor"),
            new CreditSettleCommand("task-expiry-1", 60, "cred2-boundary-settle-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, settled.Status);

        // The payment is an earned credit that never expires.
        var lots = await ledger.LotsAsync(contributor);
        var paid = Assert.Single(lots, lot => lot.AvailableAmount > 0);

        Assert.Equal(CreditLotKind.Earned, paid.Kind);
        Assert.Null(paid.ExpiresAt);
        Assert.Equal(60, paid.AvailableAmount);

        // No balance of the publisher ever went negative.
        Assert.Equal(0, await ledger.JournalBalanceAsync(publisher, LedgerBucket.UserAvailable));
        Assert.Equal(0, await ledger.JournalBalanceAsync(publisher, LedgerBucket.Reserved));
        Assert.Equal(60, await ledger.JournalBalanceAsync(contributor, LedgerBucket.UserAvailable));

        var sums = await ledger.TransactionSumsAsync();

        Assert.All(sums, sum => Assert.Equal(0, sum.Sum));
    }

    [Fact]
    public async Task Cred2BoundaryAnExpiredRefundGoesToTheExpirySinkAndNoBalanceGoesNegative()
    {
        await using var ledger = new CreditLedgerScope(_fixture);
        var publisher = await ledger.CreateUserAsync(
            "refund",
            "refund@blindmesh.test",
            ledger.Clock.UtcNow.AddDays(-30),
            "cred2-refund-publisher");

        var granted = await ledger.RunAsync(async () => await ledger.Service.GrantOnceAsync(
            new CreditActor(publisher, "refund"),
            new CreditGrantCommand(true, "cred2-refund-grant-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, granted.Status);

        var reserved = await ledger.RunAsync(async () => await ledger.Service.ReserveTaskAsync(
            new CreditActor(publisher, "refund"),
            new CreditReserveCommand(60, "task-refund-1", "cred2-refund-reserve-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, reserved.Status);

        // The grant of the publisher runs out while it waits in the reservation.
        ledger.Clock.Advance(TimeSpan.FromDays(CreditRules.GrantExpiryDays + 1));

        var refunded = await ledger.RunAsync(async () => await ledger.Service.RefundUnstartedAsync(
            new CreditActor(publisher, "refund"),
            new CreditRefundCommand("task-refund-1", 60, "cred2-refund-refund-0001"),
            CancellationToken.None));

        Assert.Equal(CreditCommandStatus.Succeeded, refunded.Status);

        // The expired credits do not come back to the publisher, they go to
        // the expiry sink.
        Assert.Equal(0, await ledger.JournalBalanceAsync(publisher, LedgerBucket.UserAvailable));
        Assert.Equal(0, await ledger.JournalBalanceAsync(publisher, LedgerBucket.Reserved));
        Assert.Equal(60, await ledger.PlatformBalanceAsync(LedgerBucket.PlatformExpiry));

        var lots = await ledger.LotsAsync(publisher);
        var spent = Assert.Single(lots);

        Assert.Equal(0, spent.AvailableAmount);
        Assert.Equal(0, spent.ReservedAmount);

        var sums = await ledger.TransactionSumsAsync();

        Assert.All(sums, sum => Assert.Equal(0, sum.Sum));
    }
}
