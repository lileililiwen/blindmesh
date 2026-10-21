using System.Net;
using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Moderation;

/// <summary>
/// MOD-1 Arbitration: a conflict free moderator decides a dispute with a
/// reason, the parties read the decision and the ledger result of it, and a
/// moderator with a conflict never touches the escrow. One case is open per
/// submission, the settlement of a seat waits for the decision, and the appeal
/// reaches another moderator without changing the rows that came before it.
/// </summary>
[Collection(ModerationCollection.Name)]
public sealed class MOD1ArbitrationTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ModerationTestHost _host = null!;

    private ModerationScenario _scenario = null!;

    public MOD1ArbitrationTests(RuntimeFixture fixture)
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
    public async Task MOD1SuccessTheIndependentModeratorDecidesTheCaseAndThePartiesSeeTheDecisionAndTheLedgerResult()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        // The escrow of the seat waits for the arbitration.
        var waiting = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, waiting.Available);
        Assert.Equal(10, waiting.Pending);
        Assert.Equal(0, waiting.Arbitrations);

        await _host.SignInAsync("dana");

        var resolved = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"reason\":\"The evidence of the seat shows the whole scope of the task.\"}",
            idempotencyKey: "mod1-success-resolve-0001");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var decision = await _host.ReadJsonAsync(resolved);

        Assert.Equal("resolved", decision.RootElement.GetProperty("state").GetString());
        Assert.True(decision.RootElement.GetProperty("award_to_contributor").GetBoolean());
        Assert.Equal(10, decision.RootElement.GetProperty("base_reward").GetInt64());
        Assert.NotEqual(Guid.Empty, decision.RootElement.GetProperty("ledger_transaction_id").GetGuid());

        var transactionId = decision.RootElement.GetProperty("ledger_transaction_id").GetGuid();

        // The parties read the same decision and the same ledger result.
        await _host.SignInAsync("bob");

        var read = await _host.SendAsync(HttpMethod.Get, $"/v1/disputes/{disputeId}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var seen = await _host.ReadJsonAsync(read);

        Assert.Equal("resolved", seen.RootElement.GetProperty("state").GetString());
        Assert.Equal(transactionId, seen.RootElement.GetProperty("ledger_transaction_id").GetGuid());

        // The base of the seat is earned by the contributor exactly once, and
        // the journal stays balanced.
        var committed = await _scenario.LedgerAsync(seat);

        Assert.Equal(10, committed.Available);
        Assert.Equal(0, committed.Pending);
        Assert.Equal(1, committed.Arbitrations);
        Assert.Equal(0, committed.Settles);
        Assert.Equal(0, committed.Compensations);
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));

        // The seat is over and the lease of the slot is released.
        var seatState = await _host.WithDatabaseAsync(async dbContext =>
        {
            var participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == seat.ParticipationId);

            var slot = await dbContext.TaskSlots.AsNoTracking()
                .FirstAsync(item => item.Id == participation.SlotId);

            var submission = await dbContext.Submissions.AsNoTracking()
                .FirstAsync(item => item.Id == submissionId);

            var leases = await dbContext.SlotLeases.AsNoTracking()
                .Where(item => item.ParticipationId == seat.ParticipationId)
                .ToListAsync();

            return new { Participation = participation, Slot = slot, Submission = submission, Leases = leases };
        });

        Assert.Equal(ParticipationState.Approved, seatState.Participation.State);
        Assert.Equal(TaskSlotState.Settled, seatState.Slot.State);
        Assert.Equal(SubmissionState.Accepted, seatState.Submission.State);
        Assert.All(seatState.Leases, lease => Assert.NotNull(lease.ReleasedAt));
    }

    [Fact]
    public async Task MOD1RejectionTheConflictingModeratorAndTheOrdinaryUserAreRefusedAndTheEscrowStaysUnchanged()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        // The publisher of the task may not decide its own case.
        await _host.SignInAsync("alice");

        var publisher = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"reason\":\"The publisher of the task decides the case of the seat.\"}",
            idempotencyKey: "mod1-rejection-publisher-0001");

        Assert.Equal(HttpStatusCode.Forbidden, publisher.StatusCode);

        // The contributor of the seat may not decide it either.
        await _host.SignInAsync("bob");

        var contributor = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"reason\":\"The contributor of the seat decides the case of the seat.\"}",
            idempotencyKey: "mod1-rejection-contributor-0001");

        Assert.Equal(HttpStatusCode.Forbidden, contributor.StatusCode);

        // An ordinary user may not decide a case at all.
        await _host.SignInAsync("cara");

        var ordinary = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"reason\":\"An ordinary user decides the case of the seat.\"}",
            idempotencyKey: "mod1-rejection-ordinary-0001");

        Assert.Equal(HttpStatusCode.Forbidden, ordinary.StatusCode);

        // The escrow stays exactly as it was and no ledger row was written.
        var unchanged = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, unchanged.Available);
        Assert.Equal(10, unchanged.Pending);
        Assert.Equal(0, unchanged.Arbitrations);
        Assert.Equal(0, unchanged.Compensations);
        Assert.Equal(30, unchanged.PublisherAvailable);
        Assert.Equal(20, unchanged.PublisherReserved);
        Assert.All(
            unchanged.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));
    }

    [Fact]
    public async Task MOD1BoundaryTheAppealReachesAnotherModeratorAndThePriorRowsStayImmutable()
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
            "{\"award_base\":true,\"reason\":\"The evidence of the seat shows the whole scope of the task.\"}",
            idempotencyKey: "mod1-boundary-resolve-0001");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var firstTransactionId = (await _host.ReadJsonAsync(resolved))
            .RootElement.GetProperty("ledger_transaction_id").GetGuid();

        var before = await _host.WithDatabaseAsync(async dbContext => new
        {
            Entries = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => entry.TransactionId == firstTransactionId)
                .OrderBy(entry => entry.Id)
                .ToListAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking()
                .Where(audit => audit.DisputeId == disputeId && audit.Action == "dispute.resolved")
                .OrderBy(audit => audit.CreatedAt)
                .ThenBy(audit => audit.Id)
                .ToListAsync(),
        });

        Assert.Single(before.Audit);
        Assert.Equal(2, before.Entries.Count);

        // The contributor appeals the decision.
        await _host.SignInAsync("bob");

        var appealed = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/disputes/{disputeId}/appeal",
            "{\"reason\":\"The evidence of the seat was never read by the owner of the task.\"}",
            idempotencyKey: "mod1-boundary-appeal-0001");

        Assert.Equal(HttpStatusCode.OK, appealed.StatusCode);

        var reopened = await _host.ReadJsonAsync(appealed);

        Assert.Equal("open", reopened.RootElement.GetProperty("state").GetString());
        Assert.Equal(seat.ContributorId, reopened.RootElement.GetProperty("appealed_by").GetGuid());

        // The moderator that already acted may not receive the appeal.
        await _host.SignInAsync("dana");

        var again = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"reason\":\"The same moderator decides the case a second time.\"}",
            idempotencyKey: "mod1-boundary-resolve-0002");

        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);

        // The appeal reached another moderator.
        var danaId = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "dana")
                .Select(user => user.Id)
                .FirstAsync());

        var assignments = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.ModeratorAssignments.AsNoTracking()
                .Where(assignment => assignment.DisputeId == disputeId)
                .ToListAsync());

        Assert.NotEmpty(assignments);
        Assert.DoesNotContain(assignments, assignment => assignment.ModeratorId == danaId);

        // The second moderator decides against the contributor. The money goes
        // back to the publisher with a compensating transaction.
        await _host.SignInAsync("cara");

        var second = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":false,\"reason\":\"The evidence of the seat does not show the whole scope.\"}",
            idempotencyKey: "mod1-boundary-resolve-0003");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var after = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, after.Available);
        Assert.Equal(0, after.Pending);
        Assert.Equal(1, after.Arbitrations);
        Assert.Equal(1, after.Compensations);
        Assert.Equal(0, after.Settles);
        Assert.Equal(40, after.PublisherAvailable);
        Assert.Equal(20, after.PublisherReserved);
        Assert.All(
            after.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));

        // The rows of the first decision are exactly as they were written.
        var afterRows = await _host.WithDatabaseAsync(async dbContext => new
        {
            Entries = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => entry.TransactionId == firstTransactionId)
                .OrderBy(entry => entry.Id)
                .ToListAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking()
                .Where(audit => audit.DisputeId == disputeId && audit.Action == "dispute.resolved")
                .OrderBy(audit => audit.CreatedAt)
                .ThenBy(audit => audit.Id)
                .ToListAsync(),
        });

        Assert.Equal(before.Entries.Count, afterRows.Entries.Count);

        for (var index = 0; index < before.Entries.Count; index++)
        {
            Assert.Equal(before.Entries[index].Id, afterRows.Entries[index].Id);
            Assert.Equal(before.Entries[index].Amount, afterRows.Entries[index].Amount);
            Assert.Equal(before.Entries[index].AccountId, afterRows.Entries[index].AccountId);
        }

        Assert.Equal(2, afterRows.Audit.Count);

        // The clock of the test stands still, so the two decisions of the case
        // carry the same moment and the rows are told apart by their
        // identifier.
        var kept = afterRows.Audit.Single(audit => audit.Id == before.Audit[0].Id);

        Assert.Equal(before.Audit[0].Reason, kept.Reason);
        Assert.Equal(before.Audit[0].ActorId, kept.ActorId);
        Assert.Equal(before.Audit[0].CreatedAt, kept.CreatedAt);
        Assert.Equal(before.Audit[0].Details, kept.Details);
    }

    [Fact]
    public async Task MOD1BoundaryOnlyOneCaseIsOpenPerSubmissionAndTheSettlementOfTheSeatWaitsForTheDecision()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        // A second case of the same seat is refused.
        await _host.SignInAsync("alice");

        var second = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/disputes",
            "{\"reason\":\"The publisher opens a second case of the same seat.\"}",
            idempotencyKey: "mod1-boundary-second-dispute-0001");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // The settlement of the seat is frozen while the case is open.
        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/accept",
            "{\"bonus\":0}",
            idempotencyKey: "mod1-boundary-accept-0001");

        Assert.Equal(HttpStatusCode.Conflict, accepted.StatusCode);

        // Only one case exists and the escrow waits.
        var open = await _host.WithDatabaseAsync(async dbContext => new
        {
            Cases = await dbContext.Disputes.AsNoTracking()
                .Where(dispute => dispute.SubmissionId == submissionId)
                .ToListAsync(),
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == seat.ParticipationId),
        });

        Assert.Single(open.Cases);
        Assert.Equal(disputeId, open.Cases[0].Id);
        Assert.Equal(ParticipationState.Disputed, open.Participation.State);

        var frozen = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, frozen.Available);
        Assert.Equal(10, frozen.Pending);
        Assert.Equal(0, frozen.Arbitrations);
        Assert.Equal(0, frozen.Settles);
    }

    [Fact]
    public async Task MOD1SuccessTheDecisionAgainstTheContributorMovesTheBaseBackToThePublisher()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        await _host.SignInAsync("dana");

        var resolved = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":false,\"reason\":\"The evidence of the seat does not show the scope of the task.\"}",
            idempotencyKey: "mod1-success-publisher-0001");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var decision = await _host.ReadJsonAsync(resolved);

        Assert.False(decision.RootElement.GetProperty("award_to_contributor").GetBoolean());

        // The base is back with the publisher and the contributor holds
        // nothing. The reserved budget of the task is untouched.
        var committed = await _scenario.LedgerAsync(seat);

        Assert.Equal(0, committed.Available);
        Assert.Equal(0, committed.Pending);
        Assert.Equal(40, committed.PublisherAvailable);
        Assert.Equal(20, committed.PublisherReserved);
        Assert.Equal(1, committed.Arbitrations);
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));

        // The seat is over and the slot is free again.
        var seatState = await _host.WithDatabaseAsync(async dbContext =>
        {
            var participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == seat.ParticipationId);

            var slot = await dbContext.TaskSlots.AsNoTracking()
                .FirstAsync(item => item.Id == participation.SlotId);

            return new { Participation = participation, Slot = slot };
        });

        Assert.Equal(ParticipationState.Declined, seatState.Participation.State);
        Assert.Equal(TaskSlotState.Available, seatState.Slot.State);
        Assert.Null(seatState.Slot.ContributorId);
    }

    [Fact]
    public async Task MOD1BoundaryTheCaseMayOnlyBeClosedAfterTheDecisionAndTheWindowOfTheAppeal()
    {
        var seat = await _scenario.RunSeatAsync();
        var submissionId = await _scenario.SubmitAsync(seat);
        var disputeId = await _scenario.OpenDisputeAsync(submissionId);

        // A case that was not decided may not be closed: the escrow is still at
        // stake.
        await _host.SignInAsync("dana");

        var early = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/close",
            "{\"reason\":\"The moderator closes a case that was not decided.\"}",
            idempotencyKey: "mod1-boundary-close-0001");

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        var resolved = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            "{\"award_base\":true,\"reason\":\"The evidence of the seat shows the whole scope of the task.\"}",
            idempotencyKey: "mod1-boundary-close-0002");

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        // The window of the appeal is still open, so the case stays open too.
        // The moderator that decided the case may not act on it again, so
        // another member of the staff closes it.
        await _host.SignInAsync("erin");

        var inWindow = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/close",
            "{\"reason\":\"The moderator closes the case before the window of the appeal has passed.\"}",
            idempotencyKey: "mod1-boundary-close-0003");

        Assert.Equal(HttpStatusCode.Conflict, inWindow.StatusCode);

        // The moderator that decided the case may not close it either.
        await _host.SignInAsync("dana");

        var decided = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/close",
            "{\"reason\":\"The moderator that decided the case closes it.\"}",
            idempotencyKey: "mod1-boundary-close-0005");

        Assert.Equal(HttpStatusCode.Forbidden, decided.StatusCode);

        // The party may still appeal the decision.
        await _host.SignInAsync("bob");

        var appealed = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/disputes/{disputeId}/appeal",
            "{\"reason\":\"The evidence of the seat was never read by the owner of the task.\"}",
            idempotencyKey: "mod1-boundary-close-0004");

        Assert.Equal(HttpStatusCode.OK, appealed.StatusCode);
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
