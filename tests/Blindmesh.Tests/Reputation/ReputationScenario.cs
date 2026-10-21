using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Reputation;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Infrastructure.Persistence.Entities;
using Blindmesh.Tests.Findings;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Reputation;

/// <summary>
/// The shared journey of the reputation tests: a published task, a seat of bob
/// that runs, the evidence of the seat and the acceptance of the owner. Every
/// step goes through the real endpoints, so the event of the outbox is written
/// inside the transaction of the command of the product.
/// </summary>
internal sealed class ReputationScenario(ReputationTestHost host)
{
    /// <summary>
    /// Runs a seat of a published task until the owner accepts the work of the
    /// seat. The acceptance writes the event of the outbox, so the consumer
    /// applies the contribution of the contributor.
    /// </summary>
    public async Task<Seat> RunAcceptedSeatAsync()
    {
        var seat = await RunSeatAsync();

        await host.SignInAsync("alice");

        var accepted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{seat.SubmissionId}/accept",
            "{\"bonus\":0}",
            idempotencyKey: $"rep-accept-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, accepted.StatusCode);

        return seat;
    }

    /// <summary>Publishes a task, gives a seat to bob and lets him send the evidence.</summary>
    public async Task<Seat> RunSeatAsync()
    {
        await host.SignInAsync("alice");
        await host.ClaimGrantAsync();

        var projectName = $"Reputation project {Guid.NewGuid():N}";
        var projectId = await host.CreateActiveProjectAsync(projectName);

        var created = await host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            FindingsTestHost.TaskBody(
                projectId,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the task page\",\"expected_result\":\"The page shows the seat.\"}"],
                deadline: FindingsTestHost.DeadlineInDays()));

        Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);

        var taskId = (await host.ReadJsonAsync(created)).RootElement.GetProperty("id").GetGuid();

        var published = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"rep-publish-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, published.StatusCode);

        var taskVersion = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.Version)
                .FirstAsync());

        await host.SignInAsync("bob");
        await host.OfferCapabilityAsync();

        var applied = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{taskVersion}}}",
            idempotencyKey: $"rep-apply-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.Created, applied.StatusCode);

        var participationId = (await host.ReadJsonAsync(applied)).RootElement.GetProperty("id").GetGuid();

        await host.SignInAsync("alice");

        var accepted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"rep-accept-seat-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, accepted.StatusCode);

        await host.SignInAsync("bob");

        var started = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: $"rep-start-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, started.StatusCode);

        var submitted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/submissions",
            """
            {
              "summary":"I walked through the seat and the scope of the task is done.",
              "scope_completed":true,
              "steps":[{"position":1,"title":"Open the task page","result":"The page shows the seat."}]
            }
            """,
            idempotencyKey: $"rep-submit-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.Created, submitted.StatusCode);

        var submissionId = (await host.ReadJsonAsync(submitted)).RootElement.GetProperty("id").GetGuid();

        var ownerId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.OwnerId)
                .FirstAsync());

        var contributorId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Participations.AsNoTracking()
                .Where(participation => participation.Id == participationId)
                .Select(participation => participation.ContributorId)
                .FirstAsync());

        return new Seat(
            taskId,
            Guid.Parse(projectId),
            participationId,
            submissionId,
            ownerId,
            contributorId,
            projectName);
    }

    /// <summary>Opens the dispute of the seat as the publisher of the task.</summary>
    public async Task<Guid> OpenDisputeAsync(Guid submissionId)
    {
        await host.SignInAsync("alice");

        var opened = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/disputes",
            "{\"reason\":\"The scope of the seat was not completed.\"}",
            idempotencyKey: $"rep-dispute-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.Created, opened.StatusCode);

        return (await host.ReadJsonAsync(opened)).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Decides the case as the moderation staff.</summary>
    public async Task ResolveDisputeAsync(string actor, Guid disputeId, bool awardToContributor)
    {
        await host.SignInAsync(actor);

        var resolved = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/admin/disputes/{disputeId}/resolve",
            awardToContributor
                ? "{\"award_base\":true,\"reason\":\"The evidence of the seat shows the whole scope of the task.\"}"
                : "{\"award_base\":false,\"reason\":\"The evidence of the seat does not show the whole scope of the task.\"}",
            idempotencyKey: $"rep-resolve-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, resolved.StatusCode);
    }

    /// <summary>Appeals the decision of the case as the contributor of the seat.</summary>
    public async Task AppealDisputeAsync(Guid disputeId)
    {
        await host.SignInAsync("bob");

        var appealed = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/disputes/{disputeId}/appeal",
            "{\"reason\":\"The evidence of the seat was never read by the owner of the task.\"}",
            idempotencyKey: $"rep-appeal-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, appealed.StatusCode);
    }

    /// <summary>
    /// Closes the project of the owner, so the source of a line of reputation
    /// is not public any more.
    /// </summary>
    public async Task CloseProjectAsync(Guid projectId)
    {
        await host.SignInAsync("alice");

        var version = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Projects.AsNoTracking()
                .Where(project => project.Id == projectId)
                .Select(project => project.Version)
                .FirstAsync());

        var closed = await host.SendAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            $"{{\"expected_version\":{version},\"visibility\":\"private\"}}");

        Assert.Equal(System.Net.HttpStatusCode.OK, closed.StatusCode);
    }

    /// <summary>
    /// The lines of the reputation of a contributor, the oldest first. The test
    /// reads the stored provenance, not only the answer of the API.
    /// </summary>
    public Task<List<ReputationEvent>> EventsAsync(Guid userId) =>
        host.WithDatabaseAsync(async dbContext => await dbContext.ReputationEvents.AsNoTracking()
            .Where(reputationEvent => reputationEvent.UserId == userId)
            .OrderBy(reputationEvent => reputationEvent.OccurredAt)
            .ThenBy(reputationEvent => reputationEvent.Id)
            .ToListAsync());

    /// <summary>The score of a contributor, when it was written.</summary>
    public Task<ReputationScore?> ScoreAsync(Guid userId) =>
        host.WithDatabaseAsync(async dbContext =>
            await dbContext.ReputationScores.AsNoTracking()
                .FirstOrDefaultAsync(score => score.UserId == userId));

    /// <summary>The messages of the outbox of a kind, the oldest first.</summary>
    public Task<List<OutboxMessageEntity>> OutboxAsync(string type) =>
        host.WithDatabaseAsync(async dbContext => await dbContext.OutboxMessages.AsNoTracking()
            .Where(message => message.Type == type)
            .OrderBy(message => message.OccurredAt)
            .ToListAsync());

    /// <summary>
    /// The journal of the ledger. The signed sum of the entries of a
    /// transaction is zero and no account of the journal holds a negative
    /// balance.
    /// </summary>
    public Task<LedgerJournal> LedgerAsync() =>
        host.WithDatabaseAsync(async dbContext =>
        {
            var entries = await dbContext.LedgerEntries.AsNoTracking().ToListAsync();
            var accounts = await dbContext.LedgerAccounts.AsNoTracking().ToListAsync();

            var balances = accounts
                .Select(account => new LedgerBalance(
                    account.Id,
                    account.Bucket,
                    account.OwnerId,
                    entries.Where(entry => entry.AccountId == account.Id).Sum(entry => entry.Amount)))
                .ToList();

            var transactions = entries
                .GroupBy(entry => entry.TransactionId)
                .Select(group => new LedgerTransactionBalance(group.Key, group.Sum(entry => entry.Amount)))
                .ToList();

            return new LedgerJournal(balances, transactions);
        });
}

/// <summary>A seat of a published task with its evidence.</summary>
internal sealed record Seat(
    Guid TaskId,
    Guid ProjectId,
    Guid ParticipationId,
    Guid SubmissionId,
    Guid OwnerId,
    Guid ContributorId,
    string ProjectName);

/// <summary>The balance of one account of the journal of the ledger.</summary>
internal sealed record LedgerBalance(Guid Id, LedgerBucket Bucket, Guid? OwnerId, long Amount);

/// <summary>The signed sum of the entries of one transaction of the journal.</summary>
internal sealed record LedgerTransactionBalance(Guid Id, long Amount);

/// <summary>The journal of the ledger of the test run.</summary>
internal sealed record LedgerJournal(
    IReadOnlyList<LedgerBalance> Balances,
    IReadOnlyList<LedgerTransactionBalance> Transactions);
