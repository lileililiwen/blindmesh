using System.Net;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Moderation;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Moderation;

/// <summary>
/// MOD-1 Arbitration and MOD-2 Safety: a moderator may award a bonus within
/// the funded escrow of the task together with the base of the seat, the appeal
/// compensates the base and the bonus, and the parties of a case read the
/// reason the case was opened.
/// </summary>
[Collection(ModerationCollection.Name)]
public sealed class MOD1BonusTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ModerationTestHost _host = null!;

    private ModerationScenario _scenario = null!;

    public MOD1BonusTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new ModerationTestHost(_fixture);
        await ResetAsync(_fixture);
        _scenario = new ModerationScenario(_host);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MOD1SuccessTheModeratorAwardsTheBaseAndTheBonusAndTheJournalStaysBalanced()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        // The escrow of the seat waits for the decision.
        var waiting = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, waiting.Available);
        Assert.Equal(10, waiting.Pending);
        Assert.Equal(30, waiting.PublisherAvailable);
        Assert.Equal(20, waiting.PublisherReserved);

        await _host.SignInAsync("dana");

        var resolved = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"bonus\":5,\"reason\":\"The evidence of the seat shows the whole scope of the task.\"}",
            idempotencyKey: "mod1-bonus-resolve-0001");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var decision = await _host.ReadJsonAsync(resolved);

        Assert.Equal("resolved", decision.RootElement.GetProperty("state").GetString());
        Assert.True(decision.RootElement.GetProperty("award_to_contributor").GetBoolean());
        Assert.Equal(15, decision.RootElement.GetProperty("awarded_amount").GetInt64());

        // The contributor earns the base of the seat and the bonus of the
        // decision, and the reserved escrow of the task keeps the rest of the
        // funded budget of the task.
        var committed = await _scenario.LedgerAsync(seat);

        Assert.Equal(15, committed.Available);
        Assert.Equal(0, committed.Pending);
        Assert.Equal(30, committed.PublisherAvailable);
        Assert.Equal(15, committed.PublisherReserved);
        Assert.Equal(1, committed.Arbitrations);
        Assert.Equal(0, committed.Compensations);
        Assert.Equal(0, committed.Settles);

        // The bonus left the reserved escrow of the publisher and landed in the
        // pending bucket of the contributor before the arbitration paid it out.
        var kinds = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.LedgerTransactions.AsNoTracking()
                .Select(transaction => transaction.Kind)
                .ToListAsync());

        Assert.Equal(2, kinds.Count(kind => kind == "begin_pending"));
        Assert.Equal(1, kinds.Count(kind => kind == "arbitration"));

        // Every transaction of the journal is balanced and no account of a
        // contributor or of a publisher goes below zero.
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));

        var userAccounts = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.LedgerAccounts.AsNoTracking()
                .Where(account => account.OwnerId != null)
                .Select(account => account.Id)
                .ToListAsync());

        Assert.All(
            committed.Entries
                .Where(entry => userAccounts.Contains(entry.AccountId))
                .GroupBy(entry => entry.AccountId),
            account => Assert.True(account.Sum(entry => entry.Amount) >= 0));
    }

    [Fact]
    public async Task MOD1RejectionTheBonusBeyondTheFundedCapIsRefusedAndTheEscrowStaysUnchanged()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        await _host.SignInAsync("dana");

        // The reserved escrow of the task holds twenty credits, so a bonus of
        // twenty five is beyond the funded cap of the case.
        var refused = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"bonus\":25,\"reason\":\"The bonus of the decision is beyond the funded escrow of the task.\"}",
            idempotencyKey: "mod1-bonus-cap-0001");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        // The escrow and the ledger stay exactly as they were.
        var unchanged = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, unchanged.Available);
        Assert.Equal(10, unchanged.Pending);
        Assert.Equal(30, unchanged.PublisherAvailable);
        Assert.Equal(20, unchanged.PublisherReserved);
        Assert.Equal(0, unchanged.Arbitrations);
        Assert.Equal(0, unchanged.Compensations);

        var kinds = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.LedgerTransactions.AsNoTracking()
                .Select(transaction => transaction.Kind)
                .ToListAsync());

        Assert.Equal(1, kinds.Count(kind => kind == "begin_pending"));
        Assert.Equal(0, kinds.Count(kind => kind == "arbitration"));

        // The case waits for a decision that the escrow can carry.
        var state = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Disputes.AsNoTracking()
                .Where(dispute => dispute.Id == disputeId)
                .Select(dispute => new { dispute.State, dispute.AwardedAmount })
                .FirstAsync());

        Assert.Equal(DisputeState.Open, state.State);
        Assert.Equal(0, state.AwardedAmount);
    }

    [Fact]
    public async Task MOD1RejectionTheBonusWithoutTheBaseIsRefusedAndNothingIsWritten()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        await _host.SignInAsync("dana");

        var refused = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":false,\"bonus\":5,\"reason\":\"The bonus of the decision has no base to go with it.\"}",
            idempotencyKey: "mod1-bonus-without-base-0001");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        var error = (await _host.ReadJsonAsync(refused)).RootElement.GetProperty("error");

        Assert.Equal("unprocessable_entity", error.GetProperty("code").GetString());
        Assert.Contains(
            "bonus",
            error.GetProperty("message").GetString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(error.GetProperty("field_errors").TryGetProperty("bonus", out _));

        // Nothing was written: the escrow, the ledger and the trail of the case
        // are exactly as they were.
        var unchanged = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, unchanged.Available);
        Assert.Equal(10, unchanged.Pending);
        Assert.Equal(30, unchanged.PublisherAvailable);
        Assert.Equal(20, unchanged.PublisherReserved);
        Assert.Equal(0, unchanged.Arbitrations);
        Assert.Equal(0, unchanged.Compensations);

        var written = await _host.WithDatabaseAsync(async dbContext => new
        {
            Kinds = await dbContext.LedgerTransactions.AsNoTracking()
                .Select(transaction => transaction.Kind)
                .ToListAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking()
                .Where(audit => audit.DisputeId == disputeId)
                .ToListAsync(),
            State = await dbContext.Disputes.AsNoTracking()
                .Where(dispute => dispute.Id == disputeId)
                .Select(dispute => dispute.State)
                .FirstAsync(),
        });

        Assert.Equal(1, written.Kinds.Count(kind => kind == "begin_pending"));
        Assert.Equal(0, written.Kinds.Count(kind => kind == "arbitration"));
        Assert.DoesNotContain(written.Audit, audit => audit.Action == "dispute.resolved");
        Assert.Equal(DisputeState.Open, written.State);
    }

    [Fact]
    public async Task MOD1BoundaryTheAppealThatReversesADecisionWithABonusMovesTheBaseAndTheBonusBack()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        // cara becomes a second moderator, so the case may reach another one.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var cara = await dbContext.Users.FirstAsync(user => user.Username == "cara");
            cara.Role = UserRole.Moderator;
            await dbContext.SaveChangesAsync();
        });

        await _host.SignInAsync("dana");

        var resolved = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"bonus\":5,\"reason\":\"The evidence of the seat shows the whole scope of the task.\"}",
            idempotencyKey: "mod1-bonus-appeal-resolve-0001");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var before = await _host.WithDatabaseAsync(async dbContext => new
        {
            Entries = await dbContext.LedgerEntries.AsNoTracking()
                .ToListAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking()
                .Where(audit => audit.DisputeId == disputeId && audit.Action == "dispute.resolved")
                .ToListAsync(),
        });

        Assert.Single(before.Audit);

        // The contributor appeals the decision.
        await _host.SignInAsync("bob");

        var appealed = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/disputes/{disputeId}/appeal",
            "{\"reason\":\"The evidence of the seat was never read by the owner of the task.\"}",
            idempotencyKey: "mod1-bonus-appeal-0001");

        Assert.Equal(HttpStatusCode.OK, appealed.StatusCode);

        // The second moderator decides against the contributor. The base and the
        // bonus go back to the publisher with a compensating transaction.
        await _host.SignInAsync("cara");

        var second = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":false,\"reason\":\"The evidence of the seat does not show the whole scope.\"}",
            idempotencyKey: "mod1-bonus-appeal-resolve-0002");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // The case remembers the amount the award moved, and the appeal
        // compensated the base and the bonus together.
        var view = await _host.SendAsync(HttpMethod.Get, $"/v1/disputes/{disputeId}");

        Assert.Equal(HttpStatusCode.OK, view.StatusCode);

        var caseView = await _host.ReadJsonAsync(view);

        Assert.Equal(15, caseView.RootElement.GetProperty("awarded_amount").GetInt64());
        Assert.Equal(15, caseView.RootElement.GetProperty("ledger_amount").GetInt64());
        Assert.False(caseView.RootElement.GetProperty("award_to_contributor").GetBoolean());

        var after = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, after.Available);
        Assert.Equal(0, after.Pending);
        Assert.Equal(45, after.PublisherAvailable);
        Assert.Equal(15, after.PublisherReserved);
        Assert.Equal(1, after.Arbitrations);
        Assert.Equal(1, after.Compensations);
        Assert.Equal(0, after.Settles);

        // The publisher holds the same total as a decision without a bonus, and
        // the reserved escrow of the task keeps the rest of the funded budget.
        Assert.Equal(60, after.PublisherAvailable + after.PublisherReserved);

        // The rows of the first decision stay exactly as they were written.
        var afterRows = await _host.WithDatabaseAsync(async dbContext => new
        {
            Entries = await dbContext.LedgerEntries.AsNoTracking()
                .ToListAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking()
                .Where(audit => audit.DisputeId == disputeId && audit.Action == "dispute.resolved")
                .ToListAsync(),
        });

        var beforeEntries = before.Entries.ToDictionary(entry => entry.Id);
        var afterEntries = afterRows.Entries.ToDictionary(entry => entry.Id);

        // The compensation wrote two new entries and changed no earlier row.
        Assert.Equal(beforeEntries.Count + 2, afterEntries.Count);

        foreach (var entry in beforeEntries.Values)
        {
            Assert.True(afterEntries.ContainsKey(entry.Id));

            Assert.Equal(entry.Amount, afterEntries[entry.Id].Amount);
            Assert.Equal(entry.AccountId, afterEntries[entry.Id].AccountId);
            Assert.Equal(entry.TransactionId, afterEntries[entry.Id].TransactionId);
        }

        // The trail of the first decision is unchanged and the appeal added one
        // more decision of the case.
        Assert.Equal(2, afterRows.Audit.Count);

        var kept = afterRows.Audit.Single(audit => audit.Id == before.Audit[0].Id);

        Assert.Equal(before.Audit[0].Reason, kept.Reason);
        Assert.Equal(before.Audit[0].ActorId, kept.ActorId);
        Assert.Equal(before.Audit[0].CreatedAt, kept.CreatedAt);
        Assert.Equal(before.Audit[0].Details, kept.Details);

        // Every transaction of the journal is balanced and no account of a
        // contributor or of a publisher goes below zero.
        Assert.All(
            after.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));

        var userAccounts = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.LedgerAccounts.AsNoTracking()
                .Where(account => account.OwnerId != null)
                .Select(account => account.Id)
                .ToListAsync());

        Assert.All(
            after.Entries
                .Where(entry => userAccounts.Contains(entry.AccountId))
                .GroupBy(entry => entry.AccountId),
            account => Assert.True(account.Sum(entry => entry.Amount) >= 0));
    }

    [Fact]
    public async Task MOD2BoundaryTheReasonOfTheCaseIsReadableByThePartiesAndTheStaffAndNotByAnUnrelatedAccount()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(
            submissionId,
            "The scope of the seat was not completed by the contributor.");

        // The publisher of the task reads the reason of the case.
        await _host.SignInAsync("alice");

        var publisher = await _host.SendAsync(HttpMethod.Get, $"/v1/disputes/{disputeId}");

        Assert.Equal(HttpStatusCode.OK, publisher.StatusCode);

        Assert.Equal(
            "The scope of the seat was not completed by the contributor.",
            (await _host.ReadJsonAsync(publisher)).RootElement.GetProperty("reason").GetString());

        // The contributor of the seat reads the same reason.
        await _host.SignInAsync("bob");

        var contributor = await _host.SendAsync(HttpMethod.Get, $"/v1/disputes/{disputeId}");

        Assert.Equal(HttpStatusCode.OK, contributor.StatusCode);

        Assert.Equal(
            "The scope of the seat was not completed by the contributor.",
            (await _host.ReadJsonAsync(contributor)).RootElement.GetProperty("reason").GetString());

        // The moderation staff reads the reason of the case.
        await _host.SignInAsync("dana");

        var moderator = await _host.SendAsync(HttpMethod.Get, $"/v1/disputes/{disputeId}");

        Assert.Equal(HttpStatusCode.OK, moderator.StatusCode);

        Assert.Equal(
            "The scope of the seat was not completed by the contributor.",
            (await _host.ReadJsonAsync(moderator)).RootElement.GetProperty("reason").GetString());

        // An unrelated account never reads the case nor its reason.
        await _host.SignInAsync("cara");

        var unrelated = await _host.SendAsync(HttpMethod.Get, $"/v1/disputes/{disputeId}");

        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);

        var body = await unrelated.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("The scope of the seat was not completed by the contributor.", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Empties the tables of the test run, so every test starts with an empty
    /// marketplace, an empty journal and no case. The roles and the bans of the
    /// seeded accounts are reset as well.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE outbox_messages, account_bans, dispute_evidence, moderation_audit, moderator_assignments, disputes, reports, attachments, finding_history, findings, submission_history, submission_revisions, submissions, participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE users SET role = CASE username WHEN 'dana' THEN 'moderator' WHEN 'erin' THEN 'administrator' ELSE 'user' END");
    }
}
