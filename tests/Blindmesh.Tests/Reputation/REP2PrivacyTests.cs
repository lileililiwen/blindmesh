using System.Net;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Reputation;

/// <summary>
/// REP-2 Privacy: the public history of a contributor holds the references a
/// visitor may read next to the safe counts, a visitor never receives a
/// private source field, and a source that becomes private disappears from the
/// later reads.
/// </summary>
[Collection(ReputationCollection.Name)]
public sealed class REP2PrivacyTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ReputationTestHost _host = null!;

    private ReputationScenario _scenario = null!;

    public REP2PrivacyTests(RuntimeFixture fixture)
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
    public async Task REP2SuccessThePublicHistoryShowsOnlyThePermittedReferences()
    {
        var seat = await _scenario.RunAcceptedSeatAsync();

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 1,
            "one stored contribution of the contributor");

        var visitor = _host.CreateClientWithoutCookies();

        var history = await visitor.GetAsync("/v1/people/bob/reputation/history");

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        var body = await history.Content.ReadAsStringAsync(CancellationToken.None);
        var historyBody = await _host.ReadJsonAsync(history);

        // The safe counts are part of the answer.
        Assert.Equal(1, historyBody.RootElement.GetProperty("current_score").GetInt32());
        Assert.Equal(1, historyBody.RootElement.GetProperty("accepted_contributions").GetInt32());
        Assert.Equal(0, historyBody.RootElement.GetProperty("confirmed_findings").GetInt32());
        Assert.Equal(0, historyBody.RootElement.GetProperty("reversed_contributions").GetInt32());

        // The line of the public source names the project and the category and
        // nothing else.
        var line = Assert.Single(historyBody.RootElement.GetProperty("events").EnumerateArray().ToList());

        Assert.Equal("accepted_contribution", line.GetProperty("category").GetString());
        Assert.Equal("Accepted contribution", line.GetProperty("label").GetString());
        Assert.Equal(1, line.GetProperty("delta").GetInt32());
        Assert.Equal(seat.ProjectName, line.GetProperty("project_title").GetString());

        // The identifiers of the source, of the seat and of the parties never
        // reach a visitor.
        Assert.False(line.TryGetProperty("task_id", out _));
        Assert.False(line.TryGetProperty("finding_id", out _));
        Assert.False(line.TryGetProperty("participation_id", out _));

        Assert.DoesNotContain(seat.TaskId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.ParticipationId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.SubmissionId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.ContributorId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.OwnerId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task REP2RejectionThePublicRequestRevealsNoPrivateField()
    {
        // The seat of the contributor belongs to a project of the publisher.
        var seat = await _scenario.RunAcceptedSeatAsync();

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 1,
            "one stored contribution of the contributor");

        // The publisher closes the project, so the source of the line is not
        // public any more.
        await _scenario.CloseProjectAsync(seat.ProjectId);

        var visitor = _host.CreateClientWithoutCookies();

        var history = await visitor.GetAsync("/v1/people/bob/reputation/history");

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        var body = await history.Content.ReadAsStringAsync(CancellationToken.None);
        var historyBody = await _host.ReadJsonAsync(history);

        // The counts stay safe, the line of the private source does not.
        Assert.Equal(1, historyBody.RootElement.GetProperty("accepted_contributions").GetInt32());
        Assert.Empty(historyBody.RootElement.GetProperty("events").EnumerateArray());

        Assert.DoesNotContain(seat.ProjectName, body, StringComparison.Ordinal);
        Assert.DoesNotContain(seat.ProjectId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.TaskId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.ParticipationId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.SubmissionId.ToString(), body, StringComparison.OrdinalIgnoreCase);

        // The export of the own history is closed to a visitor.
        var export = await visitor.GetAsync("/v1/reputation/history");

        Assert.Equal(HttpStatusCode.Unauthorized, export.StatusCode);

        var exportBody = await export.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain(seat.TaskId.ToString(), exportBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.ParticipationId.ToString(), exportBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.SubmissionId.ToString(), exportBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.ProjectName, exportBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task REP2BoundaryASourceThatBecamePrivateDisappearsFromThePublicHistory()
    {
        var seat = await _scenario.RunAcceptedSeatAsync();

        await _host.WaitForReputationAsync(
            seat.ContributorId,
            count => count == 1,
            "one stored contribution of the contributor");

        var visitor = _host.CreateClientWithoutCookies();

        // The source is public, so the line of the contributor is part of the
        // public history.
        var before = await visitor.GetAsync("/v1/people/bob/reputation/history");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        var beforeBody = await _host.ReadJsonAsync(before);
        var beforeLine = Assert.Single(beforeBody.RootElement.GetProperty("events").EnumerateArray().ToList());

        Assert.Equal(seat.ProjectName, beforeLine.GetProperty("project_title").GetString());

        // The owner of the project closes it.
        await _scenario.CloseProjectAsync(seat.ProjectId);

        // The later read of the public history does not hold the line of the
        // source any more, but the counts of the contributor stay correct.
        var after = await visitor.GetAsync("/v1/people/bob/reputation/history");

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        var afterBody = await after.Content.ReadAsStringAsync(CancellationToken.None);
        var afterDocument = await _host.ReadJsonAsync(after);

        Assert.Empty(afterDocument.RootElement.GetProperty("events").EnumerateArray());
        Assert.Equal(1, afterDocument.RootElement.GetProperty("accepted_contributions").GetInt32());
        Assert.Equal(1, afterDocument.RootElement.GetProperty("current_score").GetInt32());

        Assert.DoesNotContain(seat.ProjectName, afterBody, StringComparison.Ordinal);
        Assert.DoesNotContain(seat.ProjectId.ToString(), afterBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.TaskId.ToString(), afterBody, StringComparison.OrdinalIgnoreCase);

        // The own history of the contributor keeps the line, so the evidence of
        // the contributor is not deleted.
        await _host.SignInAsync("bob");

        var own = await _host.SendAsync(HttpMethod.Get, "/v1/reputation/history");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        var ownBody = await _host.ReadJsonAsync(own);
        var ownLine = Assert.Single(ownBody.RootElement.GetProperty("events").EnumerateArray().ToList());

        Assert.Equal("accepted_contribution", ownLine.GetProperty("category").GetString());
        Assert.Equal(seat.TaskId, ownLine.GetProperty("task_id").GetGuid());
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
