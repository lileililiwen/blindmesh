using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Findings;

/// <summary>
/// EVID-2 Review: the owner accepts the work of a seat, the pending base and
/// the granted bonus become the earned credits of the contributor and the
/// unused bonus goes back to the publisher. A rejected finding confiscates
/// nothing and a disagreement about the completion opens the arbitration. A
/// timeout that races a manual answer produces exactly one settled result.
/// </summary>
[Collection(FindingsCollection.Name)]
public sealed class EVID2ReviewTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private FindingsTestHost _host = null!;

    public EVID2ReviewTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new FindingsTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EVID2SuccessTheOwnerAcceptsTheWorkAndTheBonusIsSettled()
    {
        var seat = await RunSeatAsync();
        var submissionId = await SubmitAsync(seat);

        await _host.SignInAsync("alice");

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/accept",
            "{\"bonus\":3}",
            idempotencyKey: "evid2-success-accept-0001");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var result = await ReadJsonAsync(accepted);

        Assert.Equal("accepted", result.RootElement.GetProperty("state").GetString());
        Assert.Equal(3, result.RootElement.GetProperty("bonus_granted").GetInt64());
        Assert.Equal(13, result.RootElement.GetProperty("settled_credits").GetInt64());

        // The base and the bonus are earned, the unused bonus is back with the
        // publisher and the journal stays balanced.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == seat.ParticipationId),
            Slot = await dbContext.TaskSlots.AsNoTracking()
                .FirstAsync(slot => slot.TaskId == seat.TaskId && slot.ContributorId != null),
            Submission = await dbContext.Submissions.AsNoTracking()
                .FirstAsync(item => item.Id == submissionId),
            Available = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId &&
                        account.Bucket == LedgerBucket.UserAvailable &&
                        account.OwnerId == seat.ContributorId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId &&
                        account.Bucket == LedgerBucket.Pending &&
                        account.OwnerId == seat.ContributorId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Reserved = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId &&
                        account.Bucket == LedgerBucket.Reserved &&
                        account.OwnerId == seat.OwnerId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Settles = await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "settle"),
            Entries = await dbContext.LedgerEntries.AsNoTracking().ToListAsync(),
        });

        Assert.Equal(ParticipationState.Approved, committed.Participation.State);
        Assert.Equal(TaskSlotState.Settled, committed.Slot.State);
        Assert.Equal(SubmissionState.Accepted, committed.Submission.State);

        // 10 credits of the base and 3 credits of the bonus.
        Assert.Equal(13, committed.Available);
        Assert.Equal(0, committed.Pending);

        // The bonus of the second slot stays reserved.
        Assert.Equal(15, committed.Reserved);
        Assert.Equal(1, committed.Settles);
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));
    }

    [Fact]
    public async Task EVID2RejectionARejectedFindingConfiscatesNothingAndTheDisagreementOpensTheArbitration()
    {
        var seat = await RunSeatAsync();
        var submissionId = await SubmitAsync(seat);
        var findingId = await ReadFindingIdAsync(submissionId);

        await _host.SignInAsync("alice");

        // The owner classifies the finding. The decision touches no credit.
        var classified = await _host.SendAsync(
            HttpMethod.Patch,
            $"/v1/findings/{findingId}",
            "{\"state\":\"rejected\",\"note\":\"This is not a defect of the product.\"}",
            idempotencyKey: "evid2-rejection-finding-0001");

        Assert.Equal(HttpStatusCode.OK, classified.StatusCode);

        var finding = await ReadJsonAsync(classified);

        Assert.Equal("rejected", finding.RootElement.GetProperty("state").GetString());
        Assert.Equal(1, finding.RootElement.GetProperty("history").GetArrayLength());

        var afterDecision = await LedgerAsync(seat);

        // The base of the seat is still pending and nothing was paid out.
        Assert.Equal(0, afterDecision.Available);
        Assert.Equal(10, afterDecision.Pending);
        Assert.Equal(0, afterDecision.Settles);

        // The owner disagrees about the completion and opens the arbitration.
        var disputed = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/dispute",
            "{\"reason\":\"The scope of the seat was not completed.\"}",
            idempotencyKey: "evid2-rejection-dispute-0001");

        Assert.Equal(HttpStatusCode.OK, disputed.StatusCode);

        var submission = await ReadJsonAsync(disputed);

        Assert.Equal("disputed", submission.RootElement.GetProperty("state").GetString());
        Assert.Equal(
            "The scope of the seat was not completed.",
            submission.RootElement.GetProperty("dispute_reason").GetString());

        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == seat.ParticipationId),
            Slot = await dbContext.TaskSlots.AsNoTracking()
                .FirstAsync(slot => slot.TaskId == seat.TaskId && slot.ContributorId != null),
        });

        Assert.Equal(ParticipationState.Disputed, committed.Participation.State);
        Assert.Equal(TaskSlotState.Disputed, committed.Slot.State);

        // The funds wait for the arbitration: they are neither paid out nor
        // returned.
        var afterDispute = await LedgerAsync(seat);

        Assert.Equal(0, afterDispute.Available);
        Assert.Equal(10, afterDispute.Pending);
        Assert.Equal(0, afterDispute.Settles);
        Assert.Equal(0, afterDispute.Refunds);
    }

    [Fact]
    public async Task EVID2BoundaryTheTimeoutAcceptanceAndTheManualAcceptanceProduceOneSettledResult()
    {
        var seat = await RunSeatAsync();
        var submissionId = await SubmitAsync(seat);

        // The evidence waits longer than the review timeout.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var submission = await dbContext.Submissions.FirstAsync(item => item.Id == submissionId);

            submission.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-SubmissionRules.ReviewTimeoutHours - 1);

            await dbContext.SaveChangesAsync();
        });

        // The sweep of the worker accepts the base of the seat.
        var report = await _host.RunReviewSweepAsync();

        Assert.Equal(1, report.Accepted);

        var swept = await LedgerAsync(seat);

        Assert.Equal(10, swept.Available);
        Assert.Equal(0, swept.Pending);
        Assert.Equal(1, swept.Settles);

        var stateAfterSweep = await _host.WithDatabaseAsync(async dbContext =>
            (await dbContext.Submissions.AsNoTracking().FirstAsync(item => item.Id == submissionId)).State);

        Assert.Equal(SubmissionState.Accepted, stateAfterSweep);

        // The manual acceptance of the owner finds a settled seat and moves no
        // credit a second time.
        await _host.SignInAsync("alice");

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/accept",
            "{\"bonus\":3}",
            idempotencyKey: "evid2-boundary-accept-0001");

        Assert.Equal(HttpStatusCode.Conflict, accepted.StatusCode);

        var refused = await ReadJsonAsync(accepted);

        Assert.Equal(
            "state_conflict",
            refused.RootElement.GetProperty("error").GetProperty("code").GetString());

        // A second sweep does not pay the seat twice.
        var second = await _host.RunReviewSweepAsync();

        Assert.Equal(0, second.Accepted);

        var final = await LedgerAsync(seat);

        Assert.Equal(10, final.Available);
        Assert.Equal(0, final.Pending);
        Assert.Equal(1, final.Settles);

        // The unused bonus of the slot went back to the publisher exactly once.
        Assert.Equal(1, final.Refunds);
    }

    /// <summary>
    /// Publishes a task, gives a seat to bob and lets him start the work.
    /// </summary>
    private async Task<Seat> RunSeatAsync()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            FindingsTestHost.TaskBody(
                projectId,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the task page\",\"expected_result\":\"The page shows the seat.\"}"],
                deadline: FindingsTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = (await ReadJsonAsync(created)).RootElement.GetProperty("id").GetGuid();

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"evid2-publish-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var taskVersion = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.Version)
                .FirstAsync());

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{taskVersion}}}",
            idempotencyKey: $"evid2-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        var participationId = (await ReadJsonAsync(applied)).RootElement.GetProperty("id").GetGuid();

        await _host.SignInAsync("alice");

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"evid2-accept-seat-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: $"evid2-start-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var ownerId = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.OwnerId)
                .FirstAsync());

        var contributorId = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Participations.AsNoTracking()
                .Where(participation => participation.Id == participationId)
                .Select(participation => participation.ContributorId)
                .FirstAsync());

        return new Seat(taskId, Guid.Parse(projectId), participationId, ownerId, contributorId);
    }

    /// <summary>Sends the evidence of the seat as the contributor.</summary>
    private async Task<Guid> SubmitAsync(Seat seat)
    {
        await _host.SignInAsync("bob");

        var submitted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{seat.ParticipationId}/submissions",
            """
            {
              "summary":"I walked through the seat and found one broken flow.",
              "scope_completed":true,
              "steps":[{"position":1,"title":"Open the task page","result":"The page shows the seat."}],
              "findings":[
                {"category":"bug","severity":"medium","title":"The seat is empty","reproduction":"Open the seat and read the list.","expected_result":"The seat shows the task.","actual_result":"The seat is empty."}
              ]
            }
            """,
            idempotencyKey: $"evid2-submit-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        return (await ReadJsonAsync(submitted)).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>The id of the first finding of the submission.</summary>
    private async Task<Guid> ReadFindingIdAsync(Guid submissionId)
    {
        await _host.SignInAsync("alice");

        var read = await _host.SendAsync(HttpMethod.Get, $"/v1/submissions/{submissionId}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var detail = await ReadJsonAsync(read);

        return detail.RootElement.GetProperty("findings")[0].GetProperty("id").GetGuid();
    }

    /// <summary>The state of the journal of the seat.</summary>
    private Task<Journal> LedgerAsync(Seat seat) =>
        _host.WithDatabaseAsync(async dbContext => new Journal(
            await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId &&
                        account.Bucket == LedgerBucket.UserAvailable &&
                        account.OwnerId == seat.ContributorId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId &&
                        account.Bucket == LedgerBucket.Pending &&
                        account.OwnerId == seat.ContributorId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "settle"),
            await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "refund")));

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonDocument.Parse(body);
    }

    /// <summary>A running seat of a published task.</summary>
    private sealed record Seat(
        Guid TaskId,
        Guid ProjectId,
        Guid ParticipationId,
        Guid OwnerId,
        Guid ContributorId);

    /// <summary>The state of the journal of a seat.</summary>
    private sealed record Journal(long Available, long Pending, int Settles, int Refunds);

    /// <summary>
    /// Empties the tables of the test run, so every test starts with an empty
    /// marketplace, an empty journal and no evidence.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE attachments, finding_history, findings, submission_history, submission_revisions, submissions, participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");
    }
}
