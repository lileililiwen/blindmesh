using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Identity;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Identity;

namespace Blindmesh.Application.Credits;

/// <summary>
/// The credit service of the product. It owns the journal, the lots and the
/// limited grant. Every write happens inside the transaction of the caller,
/// and every balance is rebuilt from the journal, so the journal is the only
/// truth of the credits.
/// </summary>
public sealed class CreditService(ICreditStore store, IIdentityStore identity, IClock clock)
    : ICreditService
{
    private const string KindGrant = "grant";
    private const string KindReserve = "reserve";
    private const string KindBeginPending = "begin_pending";
    private const string KindSettle = "settle";
    private const string KindRefund = "refund";
    private const string KindExpiry = "expiry";

    private const string SourceGrant = "grant";
    private const string SourcePending = "pending";
    private const string SourceSettlement = "settlement";

    public async Task<CreditWalletView?> GetWalletAsync(
        CreditActor actor,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        if (!MaySee(actor, ownerId))
        {
            return null;
        }

        var now = clock.UtcNow;

        // The expiry of the lots that ran out is written to the journal, so the
        // balance of the wallet and the balance of the journal are the same.
        await ExpireLotsAsync(ownerId, now, cancellationToken);

        return await BuildWalletAsync(ownerId, now, cancellationToken);
    }

    public async Task<CreditTransactionPage?> ListTransactionsAsync(
        CreditActor actor,
        Guid ownerId,
        int limit,
        CreditCursor? cursor,
        CancellationToken cancellationToken)
    {
        if (!MaySee(actor, ownerId))
        {
            return null;
        }

        var accounts = await store.ListAccountsAsync(ownerId, cancellationToken);
        var accountIds = accounts.Select(account => account.Id).ToList();
        var buckets = accounts.ToDictionary(account => account.Id, account => account.Bucket);

        var transactions = await store.ListTransactionsAsync(accountIds, limit, cursor, cancellationToken);
        var hasMore = transactions.Count > limit;

        if (hasMore)
        {
            transactions = transactions.Take(limit).ToList();
        }

        var entries = transactions.Count == 0
            ? []
            : await store.ListEntriesAsync(
                transactions.Select(transaction => transaction.Id).ToList(),
                accountIds,
                cancellationToken);

        var items = transactions
            .Select(transaction =>
            {
                var own = entries.Where(entry => entry.TransactionId == transaction.Id).ToList();

                return new CreditTransactionView(
                    transaction.Id,
                    transaction.Kind,
                    transaction.Reference,
                    transaction.CreatedAt,
                    Summarize(own, buckets),
                    own
                        .Select(entry => new CreditEntryView(
                            entry.Id,
                            Describe(buckets[entry.AccountId]),
                            entry.Amount,
                            entry.LotId,
                            entry.CreatedAt))
                        .ToList());
            })
            .ToList();

        var nextCursor = hasMore && items.Count > 0
            ? new CreditCursor(items[^1].CreatedAt, items[^1].Id).Encode()
            : null;

        return new CreditTransactionPage(items, nextCursor);
    }

    public async Task<CreditCommandResult> GrantOnceAsync(
        CreditActor actor,
        CreditGrantCommand command,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Forbidden,
                Message: "A signed in contributor is required to claim the grant.");
        }

        var userId = actor.UserId!.Value;
        var now = clock.UtcNow;

        var user = await identity.FindUserAsync(userId, cancellationToken);

        if (user is null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.NotFound,
                Message: "The account of the contributor does not exist.");
        }

        var external = await identity.FindExternalIdentityAsync(userId, cancellationToken);

        if (external is null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.NotFound,
                Message: "The account of the contributor has no provider identity.");
        }

        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            reasons.Add(CreditReasons.EmailNotVerified);
        }

        if (now - AccountCreatedAt(user, external) < TimeSpan.FromDays(CreditRules.GrantMinAccountAgeDays))
        {
            reasons.Add(CreditReasons.AccountTooYoung);
        }

        if (!command.RulesAccepted)
        {
            return new CreditCommandResult(
                CreditCommandStatus.ValidationFailed,
                Reasons: reasons,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["rules_accepted"] = [CreditReasons.RulesNotAccepted],
                },
                Message: CreditReasons.RulesNotAccepted);
        }

        if (reasons.Count > 0)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Forbidden,
                Reasons: reasons,
                Message: reasons[0]);
        }

        var claim = await store.FindGrantClaimAsync(external.Provider, external.Subject, cancellationToken);

        if (claim is not null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Reasons: [CreditReasons.AlreadyClaimed],
                Message: CreditReasons.AlreadyClaimed);
        }

        // The accounts are locked in the order of their identifiers. The lock
        // of the platform account holds the daily cap of the platform, the
        // lock of the ordinary account of the contributor holds the claim of
        // the subject. Two claims of the same subject therefore issue one
        // grant.
        var accounts = await PrepareAccountsAsync(
            [
                new AccountKey(LedgerBucket.UserAvailable, userId, string.Empty),
                new AccountKey(LedgerBucket.PlatformIssuance, null, CreditRules.PlatformReference),
            ],
            now,
            cancellationToken);

        await store.LockAccountsAsync(accounts.Values.ToList(), cancellationToken);

        var availableAccount = accounts[new AccountKey(LedgerBucket.UserAvailable, userId, string.Empty)];
        var issuanceAccount = accounts[new AccountKey(LedgerBucket.PlatformIssuance, null, CreditRules.PlatformReference)];

        claim = await store.FindGrantClaimAsync(external.Provider, external.Subject, cancellationToken);

        if (claim is not null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Reasons: [CreditReasons.AlreadyClaimed],
                Message: CreditReasons.AlreadyClaimed);
        }

        var since = now.AddHours(-CreditRules.GrantWindowHours);
        var issued = await store.SumIssuedSinceAsync(since, cancellationToken);

        if (issued + CreditRules.GrantAmount > CreditRules.GrantDailyGlobalCap)
        {
            return new CreditCommandResult(
                CreditCommandStatus.LimitReached,
                Reasons: [CreditReasons.DailyCapReached],
                Message: CreditReasons.DailyCapReached);
        }

        if (!string.IsNullOrWhiteSpace(command.IpAddress))
        {
            var claims = await store.CountClaimsSinceAsync(command.IpAddress, since, cancellationToken);

            if (claims >= CreditRules.GrantDailyAddressLimit)
            {
                return new CreditCommandResult(
                    CreditCommandStatus.LimitReached,
                    Reasons: [CreditReasons.AddressLimitReached],
                    Message: CreditReasons.AddressLimitReached);
            }
        }

        var lot = new CreditLot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = CreditLotKind.Grant,
            Source = SourceGrant,
            Reference = string.Empty,
            OriginalAmount = CreditRules.GrantAmount,
            AvailableAmount = CreditRules.GrantAmount,
            ReservedAmount = 0,
            PendingAmount = 0,
            ExpiresAt = now.AddDays(CreditRules.GrantExpiryDays),
            CreatedAt = now,
        };

        await store.AddLotAsync(lot, cancellationToken);

        var transaction = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            Kind = KindGrant,
            Reference = CreditRules.GrantReference,
            ActorId = userId,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
        };

        await store.AddTransactionAsync(
            transaction,
            [
                NewEntry(transaction.Id, issuanceAccount, -CreditRules.GrantAmount, null, now),
                NewEntry(transaction.Id, availableAccount, CreditRules.GrantAmount, lot.Id, now),
            ],
            cancellationToken);

        await store.AddGrantClaimAsync(
            new GrantClaim
            {
                Id = Guid.NewGuid(),
                Provider = external.Provider,
                Subject = external.Subject,
                UserId = userId,
                AccountId = availableAccount,
                TransactionId = transaction.Id,
                Amount = CreditRules.GrantAmount,
                IpAddress = command.IpAddress,
                RulesAcceptedAt = now,
                IssuedAt = now,
                ExpiresAt = lot.ExpiresAt!.Value,
            },
            cancellationToken);

        var wallet = await BuildWalletAsync(userId, now, cancellationToken);

        return new CreditCommandResult(CreditCommandStatus.Succeeded, Wallet: wallet);
    }

    public async Task<CreditCommandResult> ReserveTaskAsync(
        CreditActor actor,
        CreditReserveCommand command,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Forbidden,
                Message: "A signed in publisher is required to reserve credits.");
        }

        if (!CreditRules.IsValidAmount(command.Amount))
        {
            return InvalidAmount();
        }

        if (!CreditRules.IsValidReference(command.Reference))
        {
            return InvalidReference();
        }

        var publisherId = actor.UserId!.Value;
        var reference = command.Reference.Trim();
        var now = clock.UtcNow;

        await ExpireLotsAsync(publisherId, now, cancellationToken);

        var accounts = await PrepareAccountsAsync(
            [
                new AccountKey(LedgerBucket.UserAvailable, publisherId, string.Empty),
                new AccountKey(LedgerBucket.Reserved, publisherId, reference),
            ],
            now,
            cancellationToken);

        await store.LockAccountsAsync(accounts.Values.ToList(), cancellationToken);

        var availableAccount = accounts[new AccountKey(LedgerBucket.UserAvailable, publisherId, string.Empty)];
        var reservedAccount = accounts[new AccountKey(LedgerBucket.Reserved, publisherId, reference)];

        var balance = await store.GetBalanceAsync(availableAccount, cancellationToken);

        if (balance < command.Amount)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "The account does not hold that many available credits.");
        }

        var lots = await store.ListLotsAsync(publisherId, cancellationToken);
        var allocation = AllocateAvailable(lots, command.Amount, now);

        if (allocation is null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "The account does not hold that many spendable credits.");
        }

        var transaction = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            Kind = KindReserve,
            Reference = reference,
            ActorId = publisherId,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
        };

        var entries = new List<LedgerEntry>();

        foreach (var part in allocation)
        {
            part.Lot.AvailableAmount -= part.Amount;
            part.Lot.ReservedAmount += part.Amount;
            await store.SaveLotAsync(part.Lot, cancellationToken);

            entries.Add(NewEntry(transaction.Id, availableAccount, -part.Amount, part.Lot.Id, now));
            entries.Add(NewEntry(transaction.Id, reservedAccount, part.Amount, part.Lot.Id, now));
        }

        await store.AddTransactionAsync(transaction, entries, cancellationToken);

        var wallet = await BuildWalletAsync(publisherId, now, cancellationToken);

        return new CreditCommandResult(
            CreditCommandStatus.Succeeded,
            Wallet: wallet,
            Transaction: SummarizeTransaction(
                transaction,
                entries,
                new Dictionary<Guid, LedgerBucket>
                {
                    [availableAccount] = LedgerBucket.UserAvailable,
                    [reservedAccount] = LedgerBucket.Reserved,
                }));
    }

    public async Task<CreditCommandResult> BeginPendingAsync(
        CreditActor actor,
        CreditPendingCommand command,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Forbidden,
                Message: "A signed in publisher is required to start the work.");
        }

        if (!CreditRules.IsValidAmount(command.Amount))
        {
            return InvalidAmount();
        }

        if (!CreditRules.IsValidReference(command.Reference))
        {
            return InvalidReference();
        }

        var publisherId = actor.UserId!.Value;
        var reference = command.Reference.Trim();
        var now = clock.UtcNow;

        var accounts = await PrepareAccountsAsync(
            [
                new AccountKey(LedgerBucket.Reserved, publisherId, reference),
                new AccountKey(LedgerBucket.Pending, command.ContributorId, reference),
            ],
            now,
            cancellationToken);

        await store.LockAccountsAsync(accounts.Values.ToList(), cancellationToken);

        var reservedAccount = accounts[new AccountKey(LedgerBucket.Reserved, publisherId, reference)];
        var pendingAccount = accounts[new AccountKey(LedgerBucket.Pending, command.ContributorId, reference)];

        var balance = await store.GetBalanceAsync(reservedAccount, cancellationToken);

        if (balance < command.Amount)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "The task does not hold that many reserved credits.");
        }

        var lots = await store.ListLotsAsync(publisherId, cancellationToken);
        var allocation = AllocateReserved(lots, command.Amount);

        if (allocation is null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "The task does not hold that many reserved credits.");
        }

        var pendingLot = await store.FindPendingLotAsync(command.ContributorId, reference, cancellationToken);
        var isNewLot = pendingLot is null;

        pendingLot ??= new CreditLot
        {
            Id = Guid.NewGuid(),
            UserId = command.ContributorId,
            Kind = CreditLotKind.Earned,
            Source = SourcePending,
            Reference = reference,
            OriginalAmount = 0,
            AvailableAmount = 0,
            ReservedAmount = 0,
            PendingAmount = 0,
            ExpiresAt = null,
            CreatedAt = now,
        };

        pendingLot.OriginalAmount += command.Amount;
        pendingLot.PendingAmount += command.Amount;

        if (isNewLot)
        {
            await store.AddLotAsync(pendingLot, cancellationToken);
        }
        else
        {
            await store.SaveLotAsync(pendingLot, cancellationToken);
        }

        var transaction = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            Kind = KindBeginPending,
            Reference = reference,
            ActorId = publisherId,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
        };

        var entries = new List<LedgerEntry>();

        foreach (var part in allocation)
        {
            part.Lot.ReservedAmount -= part.Amount;
            await store.SaveLotAsync(part.Lot, cancellationToken);

            entries.Add(NewEntry(transaction.Id, reservedAccount, -part.Amount, part.Lot.Id, now));
            entries.Add(NewEntry(transaction.Id, pendingAccount, part.Amount, pendingLot.Id, now));
        }

        await store.AddTransactionAsync(transaction, entries, cancellationToken);

        return new CreditCommandResult(CreditCommandStatus.Succeeded);
    }

    public async Task<CreditCommandResult> SettleAsync(
        CreditActor actor,
        CreditSettleCommand command,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Forbidden,
                Message: "A signed in contributor is required to be paid out.");
        }

        if (!CreditRules.IsValidAmount(command.Amount))
        {
            return InvalidAmount();
        }

        if (!CreditRules.IsValidReference(command.Reference))
        {
            return InvalidReference();
        }

        var contributorId = actor.UserId!.Value;
        var reference = command.Reference.Trim();
        var now = clock.UtcNow;

        var accounts = await PrepareAccountsAsync(
            [
                new AccountKey(LedgerBucket.Pending, contributorId, reference),
                new AccountKey(LedgerBucket.UserAvailable, contributorId, string.Empty),
            ],
            now,
            cancellationToken);

        await store.LockAccountsAsync(accounts.Values.ToList(), cancellationToken);

        var pendingAccount = accounts[new AccountKey(LedgerBucket.Pending, contributorId, reference)];
        var availableAccount = accounts[new AccountKey(LedgerBucket.UserAvailable, contributorId, string.Empty)];

        var balance = await store.GetBalanceAsync(pendingAccount, cancellationToken);

        if (balance < command.Amount)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "There are not that many pending credits.");
        }

        var pendingLot = await store.FindPendingLotAsync(contributorId, reference, cancellationToken);

        if (pendingLot is null || pendingLot.PendingAmount < command.Amount)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "There are not that many pending credits.");
        }

        pendingLot.PendingAmount -= command.Amount;
        await store.SaveLotAsync(pendingLot, cancellationToken);

        // The payment is an earned credit. It never expires, also when the
        // grant of the publisher expired while it was reserved.
        var earnedLot = new CreditLot
        {
            Id = Guid.NewGuid(),
            UserId = contributorId,
            Kind = CreditLotKind.Earned,
            Source = SourceSettlement,
            Reference = string.Empty,
            OriginalAmount = command.Amount,
            AvailableAmount = command.Amount,
            ReservedAmount = 0,
            PendingAmount = 0,
            ExpiresAt = null,
            CreatedAt = now,
        };

        await store.AddLotAsync(earnedLot, cancellationToken);

        var transaction = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            Kind = KindSettle,
            Reference = reference,
            ActorId = contributorId,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
        };

        var entries = new List<LedgerEntry>
        {
            NewEntry(transaction.Id, pendingAccount, -command.Amount, pendingLot.Id, now),
            NewEntry(transaction.Id, availableAccount, command.Amount, earnedLot.Id, now),
        };

        await store.AddTransactionAsync(transaction, entries, cancellationToken);

        var wallet = await BuildWalletAsync(contributorId, now, cancellationToken);

        return new CreditCommandResult(
            CreditCommandStatus.Succeeded,
            Wallet: wallet,
            Transaction: SummarizeTransaction(
                transaction,
                entries,
                new Dictionary<Guid, LedgerBucket>
                {
                    [pendingAccount] = LedgerBucket.Pending,
                    [availableAccount] = LedgerBucket.UserAvailable,
                }));
    }

    public async Task<CreditCommandResult> RefundUnstartedAsync(
        CreditActor actor,
        CreditRefundCommand command,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Forbidden,
                Message: "A signed in publisher is required to refund a reservation.");
        }

        if (!CreditRules.IsValidAmount(command.Amount))
        {
            return InvalidAmount();
        }

        if (!CreditRules.IsValidReference(command.Reference))
        {
            return InvalidReference();
        }

        var publisherId = actor.UserId!.Value;
        var reference = command.Reference.Trim();
        var now = clock.UtcNow;

        var accounts = await PrepareAccountsAsync(
            [
                new AccountKey(LedgerBucket.Reserved, publisherId, reference),
                new AccountKey(LedgerBucket.UserAvailable, publisherId, string.Empty),
                new AccountKey(LedgerBucket.PlatformExpiry, null, CreditRules.PlatformReference),
            ],
            now,
            cancellationToken);

        await store.LockAccountsAsync(accounts.Values.ToList(), cancellationToken);

        var reservedAccount = accounts[new AccountKey(LedgerBucket.Reserved, publisherId, reference)];
        var availableAccount = accounts[new AccountKey(LedgerBucket.UserAvailable, publisherId, string.Empty)];
        var expiryAccount = accounts[new AccountKey(LedgerBucket.PlatformExpiry, null, CreditRules.PlatformReference)];

        var balance = await store.GetBalanceAsync(reservedAccount, cancellationToken);

        if (balance < command.Amount)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "The task does not hold that many reserved credits.");
        }

        var lots = await store.ListLotsAsync(publisherId, cancellationToken);
        var allocation = AllocateReserved(lots, command.Amount);

        if (allocation is null)
        {
            return new CreditCommandResult(
                CreditCommandStatus.Conflict,
                Message: "The task does not hold that many reserved credits.");
        }

        var transaction = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            Kind = KindRefund,
            Reference = reference,
            ActorId = publisherId,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
        };

        var entries = new List<LedgerEntry>();

        foreach (var part in allocation)
        {
            var expired = part.Lot.ExpiresAt.HasValue && part.Lot.ExpiresAt.Value <= now;

            part.Lot.ReservedAmount -= part.Amount;

            if (!expired)
            {
                part.Lot.AvailableAmount += part.Amount;
            }

            await store.SaveLotAsync(part.Lot, cancellationToken);

            entries.Add(NewEntry(transaction.Id, reservedAccount, -part.Amount, part.Lot.Id, now));

            if (expired)
            {
                // The grant ran out while it was reserved. The credits go to
                // the expiry sink, so no balance ever goes negative.
                entries.Add(NewEntry(transaction.Id, expiryAccount, part.Amount, null, now));
            }
            else
            {
                entries.Add(NewEntry(transaction.Id, availableAccount, part.Amount, part.Lot.Id, now));
            }
        }

        await store.AddTransactionAsync(transaction, entries, cancellationToken);

        var wallet = await BuildWalletAsync(publisherId, now, cancellationToken);

        return new CreditCommandResult(
            CreditCommandStatus.Succeeded,
            Wallet: wallet,
            Transaction: SummarizeTransaction(
                transaction,
                entries,
                new Dictionary<Guid, LedgerBucket>
                {
                    [reservedAccount] = LedgerBucket.Reserved,
                    [availableAccount] = LedgerBucket.UserAvailable,
                    [expiryAccount] = LedgerBucket.PlatformExpiry,
                }));
    }

    /// <summary>
    /// Writes the expiry of the lots that ran out. The credits of an expired
    /// lot leave the ordinary bucket and go to the expiry sink, so the balance
    /// of the journal and the balance of the wallet stay the same.
    /// </summary>
    private async Task ExpireLotsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lots = await store.ListLotsAsync(userId, cancellationToken);
        var expired = lots
            .Where(lot => lot.Kind == CreditLotKind.Grant
                && lot.ExpiresAt.HasValue
                && lot.ExpiresAt.Value <= now
                && lot.AvailableAmount > 0)
            .ToList();

        if (expired.Count == 0)
        {
            return;
        }

        var accounts = await PrepareAccountsAsync(
            [
                new AccountKey(LedgerBucket.UserAvailable, userId, string.Empty),
                new AccountKey(LedgerBucket.PlatformExpiry, null, CreditRules.PlatformReference),
            ],
            now,
            cancellationToken);

        await store.LockAccountsAsync(accounts.Values.ToList(), cancellationToken);

        var availableAccount = accounts[new AccountKey(LedgerBucket.UserAvailable, userId, string.Empty)];
        var expiryAccount = accounts[new AccountKey(LedgerBucket.PlatformExpiry, null, CreditRules.PlatformReference)];

        var current = await store.ListLotsAsync(userId, cancellationToken);

        foreach (var lot in current.Where(lot =>
            lot.Kind == CreditLotKind.Grant
            && lot.ExpiresAt.HasValue
            && lot.ExpiresAt.Value <= now
            && lot.AvailableAmount > 0))
        {
            var amount = lot.AvailableAmount;
            lot.AvailableAmount = 0;
            await store.SaveLotAsync(lot, cancellationToken);

            var transaction = new LedgerTransaction
            {
                Id = Guid.NewGuid(),
                Kind = KindExpiry,
                Reference = CreditRules.GrantReference,
                ActorId = null,
                CreatedAt = now,
            };

            await store.AddTransactionAsync(
                transaction,
                [
                    NewEntry(transaction.Id, availableAccount, -amount, lot.Id, now),
                    NewEntry(transaction.Id, expiryAccount, amount, null, now),
                ],
                cancellationToken);
        }
    }

    private async Task<CreditWalletView> BuildWalletAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accounts = await store.ListAccountsAsync(userId, cancellationToken);
        var balances = await store.GetBalancesAsync(accounts.Select(account => account.Id).ToList(), cancellationToken);

        long available = 0;
        long reserved = 0;
        long pending = 0;

        foreach (var account in accounts)
        {
            var balance = balances.TryGetValue(account.Id, out var value) ? value : 0;

            switch (account.Bucket)
            {
                case LedgerBucket.UserAvailable:
                    available += balance;
                    break;
                case LedgerBucket.Reserved:
                    reserved += balance;
                    break;
                case LedgerBucket.Pending:
                    pending += balance;
                    break;
                default:
                    break;
            }
        }

        var lots = await store.ListLotsAsync(userId, cancellationToken);

        var lotViews = lots
            .Where(lot => lot.AvailableAmount > 0 || lot.ReservedAmount > 0 || lot.PendingAmount > 0)
            .Select(lot => new CreditLotView(
                lot.Id,
                Describe(lot.Kind),
                lot.Source,
                lot.OriginalAmount,
                lot.AvailableAmount,
                lot.ReservedAmount,
                lot.PendingAmount,
                lot.ExpiresAt,
                lot.CreatedAt,
                lot.ExpiresAt.HasValue && lot.ExpiresAt.Value <= now))
            .OrderBy(lot => lot.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(lot => lot.CreatedAt)
            .ToList();

        return new CreditWalletView(
            userId,
            available,
            reserved,
            pending,
            lotViews,
            await BuildGrantViewAsync(userId, now, cancellationToken),
            now);
    }

    private async Task<CreditGrantView> BuildGrantViewAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        DateTimeOffset? claimedAt = null;
        DateTimeOffset? expiresAt = null;

        var user = await identity.FindUserAsync(userId, cancellationToken);
        var external = user is null ? null : await identity.FindExternalIdentityAsync(userId, cancellationToken);

        if (user is not null && string.IsNullOrWhiteSpace(user.Email))
        {
            reasons.Add(CreditReasons.EmailNotVerified);
        }

        if (user is not null && now - AccountCreatedAt(user, external) < TimeSpan.FromDays(CreditRules.GrantMinAccountAgeDays))
        {
            reasons.Add(CreditReasons.AccountTooYoung);
        }

        if (external is not null)
        {
            var claim = await store.FindGrantClaimAsync(external.Provider, external.Subject, cancellationToken);

            if (claim is not null)
            {
                reasons.Add(CreditReasons.AlreadyClaimed);
                claimedAt = claim.IssuedAt;
                expiresAt = claim.ExpiresAt;
            }
        }

        var since = now.AddHours(-CreditRules.GrantWindowHours);
        var issued = await store.SumIssuedSinceAsync(since, cancellationToken);

        if (issued + CreditRules.GrantAmount > CreditRules.GrantDailyGlobalCap)
        {
            reasons.Add(CreditReasons.DailyCapReached);
        }

        return new CreditGrantView(
            reasons.Count == 0,
            CreditRules.GrantAmount,
            CreditRules.GrantExpiryDays,
            CreditRules.GrantMinAccountAgeDays,
            reasons,
            claimedAt,
            expiresAt);
    }

    /// <summary>
    /// Creates the accounts of a command and returns their identifiers. The
    /// accounts are created in the order the database locks them, so two
    /// commands never wait for each other.
    /// </summary>
    /// <summary>
    /// The moment the account of the contributor was created. The provider
    /// owns the age of its account, so the stored date of the provider
    /// identity decides it. The creation of the product account is the
    /// fallback for a provider that reports no date.
    /// </summary>
    private static DateTimeOffset AccountCreatedAt(User user, ExternalIdentity? external) =>
        external?.AccountCreatedAt ?? user.CreatedAt;

    private async Task<IReadOnlyDictionary<AccountKey, Guid>> PrepareAccountsAsync(
        IReadOnlyList<AccountKey> keys,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ordered = keys
            .Select(key => (Key: key, Id: CreditAccountIds.Derive(key.Bucket, key.OwnerId, key.Reference)))
            .OrderBy(item => CreditAccountIds.Order(item.Id), StringComparer.Ordinal)
            .ToList();

        var accounts = new Dictionary<AccountKey, Guid>();

        foreach (var item in ordered)
        {
            await store.EnsureAccountAsync(item.Key.Bucket, item.Key.OwnerId, item.Key.Reference, now, cancellationToken);
            accounts[item.Key] = item.Id;
        }

        return accounts;
    }

    /// <summary>
    /// The lots that pay an amount. The grant lots with the oldest expiry come
    /// first, the earned lots come last. Null means that the amount is not
    /// spendable.
    /// </summary>
    private static IReadOnlyList<LotPart>? AllocateAvailable(
        IReadOnlyList<CreditLot> lots,
        long amount,
        DateTimeOffset now)
    {
        var candidates = lots
            .Where(lot => lot.AvailableAmount > 0 && (!lot.ExpiresAt.HasValue || lot.ExpiresAt.Value > now))
            .OrderBy(lot => lot.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(lot => lot.CreatedAt)
            .ToList();

        return Take(candidates, amount, lot => lot.AvailableAmount);
    }

    /// <summary>
    /// The lots that hold reserved credits of a task. The grant lots with the
    /// oldest expiry come first, so the credits that run out first are the
    /// ones that leave the reservation first.
    /// </summary>
    private static IReadOnlyList<LotPart>? AllocateReserved(IReadOnlyList<CreditLot> lots, long amount)
    {
        var candidates = lots
            .Where(lot => lot.ReservedAmount > 0)
            .OrderBy(lot => lot.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(lot => lot.CreatedAt)
            .ToList();

        return Take(candidates, amount, lot => lot.ReservedAmount);
    }

    private static IReadOnlyList<LotPart>? Take(
        IReadOnlyList<CreditLot> candidates,
        long amount,
        Func<CreditLot, long> selector)
    {
        var parts = new List<LotPart>();
        var remaining = amount;

        foreach (var lot in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var available = selector(lot);

            if (available <= 0)
            {
                continue;
            }

            var part = Math.Min(available, remaining);
            parts.Add(new LotPart(lot, part));
            remaining -= part;
        }

        return remaining > 0 ? null : parts;
    }

    private static LedgerEntry NewEntry(
        Guid transactionId,
        Guid accountId,
        long amount,
        Guid? lotId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            AccountId = accountId,
            Amount = amount,
            LotId = lotId,
            CreatedAt = now,
        };

    private static CreditTransactionView SummarizeTransaction(
        LedgerTransaction transaction,
        IReadOnlyList<LedgerEntry> entries,
        IReadOnlyDictionary<Guid, LedgerBucket> buckets) =>
        new(
            transaction.Id,
            transaction.Kind,
            transaction.Reference,
            transaction.CreatedAt,
            Summarize(entries, buckets),
            entries
                .Select(entry => new CreditEntryView(
                    entry.Id,
                    Describe(buckets[entry.AccountId]),
                    entry.Amount,
                    entry.LotId,
                    entry.CreatedAt))
                .ToList());

    private static IReadOnlyList<CreditBalanceChangeView> Summarize(
        IEnumerable<LedgerEntry> entries,
        IReadOnlyDictionary<Guid, LedgerBucket> buckets)
    {
        return entries
            .GroupBy(entry => buckets[entry.AccountId])
            .Select(group => new CreditBalanceChangeView(Describe(group.Key), group.Sum(entry => entry.Amount)))
            .OrderBy(change => change.Bucket, StringComparer.Ordinal)
            .ToList();
    }

    private static bool MaySee(CreditActor actor, Guid ownerId) =>
        actor.IsAuthenticated && actor.UserId!.Value == ownerId;

    private static CreditCommandResult InvalidAmount() =>
        new(
            CreditCommandStatus.ValidationFailed,
            FieldErrors: new Dictionary<string, string[]>
            {
                ["amount"] = ["The amount must be a positive number of credits."],
            },
            Message: "The amount must be a positive number of credits.");

    private static CreditCommandResult InvalidReference() =>
        new(
            CreditCommandStatus.ValidationFailed,
            FieldErrors: new Dictionary<string, string[]>
            {
                ["reference"] = [$"The reference must hold 1 to {CreditRules.ReferenceMaxLength} characters."],
            },
            Message: $"The reference must hold 1 to {CreditRules.ReferenceMaxLength} characters.");

    private static string Describe(LedgerBucket bucket) => bucket switch
    {
        LedgerBucket.UserAvailable => "available",
        LedgerBucket.Reserved => "reserved",
        LedgerBucket.Pending => "pending",
        LedgerBucket.PlatformIssuance => "issuance",
        LedgerBucket.PlatformExpiry => "expiry",
        _ => "other",
    };

    private static string Describe(CreditLotKind kind) => kind switch
    {
        CreditLotKind.Grant => "grant",
        CreditLotKind.Earned => "earned",
        _ => "other",
    };

    private sealed record AccountKey(LedgerBucket Bucket, Guid? OwnerId, string Reference);

    private sealed record LotPart(CreditLot Lot, long Amount);
}
