using System.Net;
using Blindmesh.Application.Findings;
using Blindmesh.Domain.Reputation;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Reputation;

/// <summary>
/// REP-1 Provenance: an accepted contribution of a contributor is counted once
/// and the profile explains the category of its source, the same event that
/// arrives a second time changes nothing, and an arbitration that reverses an
/// accepted contribution corrects the projection without deleting the evidence
/// of the past.
/// </summary>
[Collection(ReputationCollection.Name)]
public sealed class REP1ProvenanceTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ReputationTestHost _host = null!;

    private ReputationScenario _scenario = null!;

    public REP1ProvenanceTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new ReputationTestHost(_fixture);
        await ResetAsync(_fixture);
        _scenario = new ReputationScenario(_host);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task REP1SuccessTheAcceptedContributionIsCountedOnceAndTheProfileExplainsItsCategory()
    {
        var seat = await _scenario.RunAcceptedSeatAsync();

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 1,
            "one stored contribution of the contributor");

        // The provenance of the contribution is stored, not guessed.
        var events = await _scenario.EventsAsync(seat.ContributorId);
        var stored = Assert.Single(events);

        Assert.Equal(ReputationCategory.AcceptedContribution, stored.Category);
        Assert.Equal(1, stored.Delta);
        Assert.Equal(seat.ProjectId, stored.SourceProjectId);
        Assert.Equal(seat.TaskId, stored.SourceTaskId);
        Assert.Equal(seat.ParticipationId, stored.SourceParticipationId);
        Assert.Equal(seat.SubmissionId, stored.SourceSubmissionId);
        Assert.Equal("Accepted contribution", stored.Note);

        // The score counts the contribution once.
        var score = await _scenario.ScoreAsync(seat.ContributorId);

        Assert.NotNull(score);
        Assert.Equal(1, score!.CurrentScore);
        Assert.Equal(1, score.AcceptedContributions);
        Assert.Equal(0, score.ConfirmedFindings);
        Assert.Equal(0, score.ReversedContributions);

        // The public profile of the contributor explains the source of the count.
        var visitor = _host.CreateClientWithoutCookies();

        var summary = await visitor.GetAsync("/v1/people/bob/reputation");

        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);

        var summaryBody = await _host.ReadJsonAsync(summary);

        Assert.Equal("bob", summaryBody.RootElement.GetProperty("username").GetString());
        Assert.Equal(1, summaryBody.RootElement.GetProperty("current_score").GetInt32());
        Assert.Equal(1, summaryBody.RootElement.GetProperty("accepted_contributions").GetInt32());

        var history = await visitor.GetAsync("/v1/people/bob/reputation/history");

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        var historyBody = await _host.ReadJsonAsync(history);

        Assert.Equal(1, historyBody.RootElement.GetProperty("current_score").GetInt32());
        Assert.Equal(1, historyBody.RootElement.GetProperty("accepted_contributions").GetInt32());

        var line = Assert.Single(historyBody.RootElement.GetProperty("events").EnumerateArray().ToList());

        Assert.Equal("accepted_contribution", line.GetProperty("category").GetString());
        Assert.Equal("Accepted contribution", line.GetProperty("label").GetString());
        Assert.Equal(1, line.GetProperty("delta").GetInt32());
        Assert.Equal(seat.ProjectName, line.GetProperty("project_title").GetString());
    }

    [Fact]
    public async Task REP1RejectionTheSameEventDeliveredTwiceChangesNothing()
    {
        var seat = await _scenario.RunAcceptedSeatAsync();

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 1,
            "one stored contribution of the contributor");

        var before = Assert.Single(await _scenario.EventsAsync(seat.ContributorId));
        var scoreBefore = await _scenario.ScoreAsync(seat.ContributorId);

        Assert.NotNull(scoreBefore);

        var message = Assert.Single(await _scenario.OutboxAsync(FindingsEventTypes.ParticipationAccepted));

        Assert.Equal(1, message.Attempts);

        // The same message of the outbox is delivered a second time.
        await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE outbox_messages SET processed_at = NULL WHERE id = {0}",
                message.Id));

        await _host.WaitForRedeliveryAsync(message.Id, 2);

        // The count and the stored provenance did not move.
        var after = Assert.Single(await _scenario.EventsAsync(seat.ContributorId));
        var scoreAfter = await _scenario.ScoreAsync(seat.ContributorId);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.SourceEventId, after.SourceEventId);
        Assert.Equal(before.Category, after.Category);
        Assert.Equal(before.Delta, after.Delta);
        Assert.Equal(before.OccurredAt, after.OccurredAt);
        Assert.Equal(before.Note, after.Note);
        Assert.Equal(before.SourceProjectId, after.SourceProjectId);
        Assert.Equal(before.SourceTaskId, after.SourceTaskId);
        Assert.Equal(before.SourceParticipationId, after.SourceParticipationId);
        Assert.Equal(before.SourceSubmissionId, after.SourceSubmissionId);

        Assert.NotNull(scoreAfter);
        Assert.Equal(scoreBefore!.CurrentScore, scoreAfter!.CurrentScore);
        Assert.Equal(scoreBefore.AcceptedContributions, scoreAfter.AcceptedContributions);
        Assert.Equal(1, scoreAfter.CurrentScore);
        Assert.Equal(1, scoreAfter.AcceptedContributions);
    }

    [Fact]
    public async Task REP1BoundaryTheReversalCorrectsTheProjectionAndKeepsTheHistory()
    {
        var seat = await _scenario.RunSeatAsync();
        var disputeId = await _scenario.OpenDisputeAsync(seat.SubmissionId);

        // The first decision of the case accepts the contribution of the seat.
        await _scenario.ResolveDisputeAsync("dana", disputeId, awardToContributor: true);

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 1,
            "the accepted contribution of the contributor");

        // The contributor appeals the decision.
        await _scenario.AppealDisputeAsync(disputeId);

        // The second decision takes the award back.
        await _scenario.ResolveDisputeAsync("erin", disputeId, awardToContributor: false);

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 2,
            "the accepted contribution and the line of its reversal");

        // The history keeps the evidence of the past and the projection is
        // corrected.
        var events = await _scenario.EventsAsync(seat.ContributorId);

        Assert.Equal(2, events.Count);

        var accepted = events[0];
        var reversal = events[1];

        Assert.Equal(ReputationCategory.AcceptedContribution, accepted.Category);
        Assert.Equal(1, accepted.Delta);
        Assert.Equal(ReputationCategory.ReversedContribution, reversal.Category);
        Assert.Equal(-1, reversal.Delta);

        // The line of the reversal names the same source as the line it
        // corrects.
        Assert.Equal(accepted.SourceParticipationId, reversal.SourceParticipationId);
        Assert.Equal(accepted.SourceSubmissionId, reversal.SourceSubmissionId);
        Assert.Equal(accepted.SourceProjectId, reversal.SourceProjectId);
        Assert.Equal(accepted.SourceTaskId, reversal.SourceTaskId);

        var score = await _scenario.ScoreAsync(seat.ContributorId);

        Assert.NotNull(score);
        Assert.Equal(0, score!.CurrentScore);
        Assert.Equal(1, score.AcceptedContributions);
        Assert.Equal(1, score.ReversedContributions);

        // The journal of the ledger still balances and no balance of a
        // contributor is negative.
        var journal = await _scenario.LedgerAsync();

        Assert.NotEmpty(journal.Transactions);
        Assert.All(
            journal.Transactions,
            transaction => Assert.Equal(0, transaction.Amount));
        Assert.All(
            journal.Balances.Where(balance => balance.OwnerId is not null),
            balance => Assert.True(balance.Amount >= 0, $"The account {balance.Id} holds {balance.Amount}."));
    }

    /// <summary>
    /// Empties the tables of the test run, so every test starts with an empty
    /// marketplace, an empty journal and no line of reputation. The roles of
    /// the seeded accounts are reset as well.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE reputation_snapshots, reputation_scores, reputation_events, outbox_messages, account_bans, dispute_evidence, moderation_audit, moderator_assignments, disputes, reports, attachments, finding_history, findings, submission_history, submission_revisions, submissions, participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE users SET role = CASE username WHEN 'dana' THEN 'moderator' WHEN 'erin' THEN 'administrator' ELSE 'user' END");
    }
}
