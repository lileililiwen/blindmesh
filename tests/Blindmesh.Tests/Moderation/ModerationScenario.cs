using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Findings;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Moderation;

/// <summary>
/// The shared journey of the moderation tests: a published task, a seat of bob
/// that runs, the evidence of the seat and the dispute of the owner. Every
/// step goes through the real endpoints, so the escrow of the seat is the
/// escrow of the product.
/// </summary>
internal sealed class ModerationScenario(ModerationTestHost host)
{
    /// <summary>Publishes a task, gives a seat to bob and lets him start the work.</summary>
    public async Task<Seat> RunSeatAsync()
    {
        await host.SignInAsync("alice");
        await host.SendAsync(
            HttpMethod.Post,
            "/v1/wallet/grant",
            "{\"rules_accepted\":true}",
            idempotencyKey: $"mod-grant-{Guid.NewGuid():N}");

        var projectId = await CreateProjectAsync("alice", "Moderation project", "public");

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
            idempotencyKey: $"mod-publish-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, published.StatusCode);

        var taskVersion = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.Version)
                .FirstAsync());

        await host.SignInAsync("bob");
        await host.SendAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            "{\"kind\":\"region\",\"label\":\"eu-central\",\"tags\":[\"eu-central\"],\"visibility\":\"public\",\"available\":true}",
            idempotencyKey: $"mod-capability-{Guid.NewGuid():N}");

        var applied = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{taskVersion}}}",
            idempotencyKey: $"mod-apply-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.Created, applied.StatusCode);

        var participationId = (await host.ReadJsonAsync(applied)).RootElement.GetProperty("id").GetGuid();

        await host.SignInAsync("alice");

        var accepted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"mod-accept-seat-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, accepted.StatusCode);

        await host.SignInAsync("bob");

        var started = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: $"mod-start-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.OK, started.StatusCode);

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

        return new Seat(taskId, Guid.Parse(projectId), participationId, ownerId, contributorId);
    }

    /// <summary>Sends the evidence of the seat as the contributor.</summary>
    public async Task<Guid> SubmitAsync(Seat seat)
    {
        await host.SignInAsync("bob");

        var submitted = await host.SendAsync(
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
            idempotencyKey: $"mod-submit-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.Created, submitted.StatusCode);

        return (await host.ReadJsonAsync(submitted)).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Opens the dispute of the seat as the publisher of the task.</summary>
    public async Task<Guid> OpenDisputeAsync(Guid submissionId, string reason = "The scope of the seat was not completed.")
    {
        await host.SignInAsync("alice");

        var opened = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{submissionId}/disputes",
            $"{{\"reason\":\"{reason}\"}}",
            idempotencyKey: $"mod-dispute-{Guid.NewGuid():N}");

        Assert.Equal(System.Net.HttpStatusCode.Created, opened.StatusCode);

        return (await host.ReadJsonAsync(opened)).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Creates a project of the account and activates it.</summary>
    private async Task<string> CreateProjectAsync(string actor, string name, string visibility)
    {
        await host.SignInAsync(actor);

        var create = await host.SendAsync(
            HttpMethod.Post,
            "/v1/projects",
            $$"""
              {"name":"{{name}}","summary":"A project of the moderation tests.","description":"A project of the moderation tests.","repository_url":"https://github.com/blindmesh/moderation-tests","visibility":"{{visibility}}"}
              """);

        Assert.Equal(System.Net.HttpStatusCode.Created, create.StatusCode);

        var created = await host.ReadJsonAsync(create);
        var projectId = created.RootElement.GetProperty("id").GetString()!;
        var version = created.RootElement.GetProperty("version").GetInt32();

        var transition = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/transitions",
            $"{{\"expected_version\":{version},\"to\":\"active\"}}");

        Assert.Equal(System.Net.HttpStatusCode.OK, transition.StatusCode);

        return projectId;
    }

    /// <summary>The state of the journal of a seat.</summary>
    public Task<Journal> LedgerAsync(Seat seat) =>
        host.WithDatabaseAsync(async dbContext => new Journal(
            await SumAsync(dbContext, LedgerBucket.UserAvailable, seat.ContributorId),
            await SumAsync(dbContext, LedgerBucket.Pending, seat.ContributorId),
            await SumAsync(dbContext, LedgerBucket.UserAvailable, seat.OwnerId),
            await SumAsync(dbContext, LedgerBucket.Reserved, seat.OwnerId),
            await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "settle"),
            await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "arbitration"),
            await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "compensation"),
            await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "refund"),
            await dbContext.LedgerEntries.AsNoTracking().ToListAsync()));

    private static async Task<long> SumAsync(BlindmeshDbContext dbContext, LedgerBucket bucket, Guid ownerId) =>
        await dbContext.LedgerEntries.AsNoTracking()
            .Where(entry => dbContext.LedgerAccounts
                .Any(account => account.Id == entry.AccountId &&
                    account.Bucket == bucket &&
                    account.OwnerId == ownerId))
            .SumAsync(entry => (long?)entry.Amount) ?? 0;
}

/// <summary>A running seat of a published task.</summary>
internal sealed record Seat(
    Guid TaskId,
    Guid ProjectId,
    Guid ParticipationId,
    Guid OwnerId,
    Guid ContributorId);

/// <summary>The state of the journal of a seat.</summary>
internal sealed record Journal(
    long Available,
    long Pending,
    long PublisherAvailable,
    long PublisherReserved,
    int Settles,
    int Arbitrations,
    int Compensations,
    int Refunds,
    IReadOnlyList<LedgerEntry> Entries);
