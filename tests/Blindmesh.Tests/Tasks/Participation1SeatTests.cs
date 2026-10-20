using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Tasks;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// PART-1 Seat: an authorized owner accepts an eligible applicant for an
/// available slot, the applicant starts the work and reads his own
/// instructions and deadline. Two acceptance commands that contend for the
/// last slot produce exactly one lease. A member of the project of the task
/// never earns from that project, even when the public capabilities match.
/// </summary>
[Collection(ParticipationCollection.Name)]
public sealed class PART1SeatTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ParticipationTestHost _host = null!;

    public PART1SeatTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new ParticipationTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PART1SuccessAnOwnerAcceptsAnEligibleApplicantAndTheApplicantStartsTheWork()
    {
        var task = await PublishTaskAsync(slotCount: 2);

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await ApplyAsync("bob", task.TaskId, await VersionAsync(task.TaskId));

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        var application = await ReadJsonAsync(applied);

        Assert.Equal("applied", application.RootElement.GetProperty("state").GetString());
        Assert.Equal("bob", application.RootElement.GetProperty("contributor_username").GetString());

        // An applied seat holds no slot and no instructions.
        Assert.False(application.RootElement.GetProperty("instructions_released").GetBoolean());
        Assert.Equal(
            0,
            application.RootElement.GetProperty("steps").GetArrayLength());

        var participationId = application.RootElement.GetProperty("id").GetGuid();

        // The owner reads the applications of the task.
        await _host.SignInAsync("alice");

        var applications = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{task.TaskId}/applications");

        Assert.Equal(HttpStatusCode.OK, applications.StatusCode);

        var list = await ReadJsonAsync(applications);

        Assert.Equal(1, list.RootElement.GetProperty("items").GetArrayLength());

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: "part1-success-accept-0001");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var seat = await ReadJsonAsync(accepted);

        Assert.Equal("accepted", seat.RootElement.GetProperty("state").GetString());
        Assert.Equal("leased", seat.RootElement.GetProperty("slot_state").GetString());
        Assert.Equal(15, seat.RootElement.GetProperty("slot_reward").GetInt64());
        Assert.NotEqual(default, seat.RootElement.GetProperty("start_deadline").GetDateTimeOffset());

        // The applicant starts the work and reads his own instructions.
        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: "part1-success-start-0001");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var running = await ReadJsonAsync(started);

        Assert.Equal("running", running.RootElement.GetProperty("state").GetString());
        Assert.True(running.RootElement.GetProperty("instructions_released").GetBoolean());
        Assert.NotEmpty(running.RootElement.GetProperty("scope").GetString()!);
        Assert.Equal(1, running.RootElement.GetProperty("steps").GetArrayLength());
        Assert.NotEqual(default, running.RootElement.GetProperty("execution_deadline").GetDateTimeOffset());

        // The seat, the lease, the slot and the escrow are committed together.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == participationId),
            Leases = await dbContext.SlotLeases.AsNoTracking().ToListAsync(),
            Slots = await dbContext.TaskSlots.AsNoTracking()
                .Where(slot => slot.TaskId == Guid.Parse(task.TaskId)).ToListAsync(),
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Pending))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Reserve = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Reserved))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Entries = await dbContext.LedgerEntries.AsNoTracking().ToListAsync(),
        });

        Assert.Equal(ParticipationState.Running, committed.Participation.State);
        Assert.NotNull(committed.Participation.StartedAt);
        Assert.NotNull(committed.Participation.ExecutionDeadline);

        var lease = Assert.Single(committed.Leases);
        Assert.Null(lease.ReleasedAt);
        Assert.Equal(participationId, lease.ParticipationId);

        var leased = Assert.Single(committed.Slots, slot => slot.State != TaskSlotState.Available);
        Assert.Equal(TaskSlotState.Active, leased.State);
        Assert.NotNull(leased.StartedAt);

        // The base reward of the slot moved into the pending bucket of the
        // contributor and the journal stayed balanced.
        Assert.Equal(10, committed.Pending);
        Assert.Equal(20, committed.Reserve);
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));
    }

    [Fact]
    public async Task PART1RejectionTwoAcceptancesOfTheLastSlotProduceExactlyOneLease()
    {
        var task = await PublishTaskAsync(slotCount: 1);

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var bobApplication = await ApplyAsync("bob", task.TaskId, await VersionAsync(task.TaskId));
        var bobId = (await ReadJsonAsync(bobApplication)).RootElement.GetProperty("id").GetGuid();

        await _host.SignInAsync("dana");
        await _host.OfferCapabilityAsync();

        var danaApplication = await ApplyAsync("dana", task.TaskId, await VersionAsync(task.TaskId));
        var danaId = (await ReadJsonAsync(danaApplication)).RootElement.GetProperty("id").GetGuid();

        await _host.SignInAsync("alice");

        var first = _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{bobId}/accept",
            idempotencyKey: "part1-rejection-accept-bob");

        var second = _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{danaId}/accept",
            idempotencyKey: "part1-rejection-accept-dana");

        var responses = await Task.WhenAll(first, second);

        var accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        var refused = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, accepted);
        Assert.Equal(1, refused);

        // The loser learns that every slot of the task is taken.
        var loser = responses.First(response => response.StatusCode == HttpStatusCode.Conflict);
        var error = await ReadJsonAsync(loser);

        Assert.Equal("state_conflict", error.RootElement.GetProperty("error").GetProperty("code").GetString());

        // Exactly one lease, exactly one leased slot and exactly one
        // reservation of the budget.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Leases = await dbContext.SlotLeases.AsNoTracking().ToListAsync(),
            Live = await dbContext.SlotLeases.AsNoTracking().CountAsync(lease => lease.ReleasedAt == null),
            Leased = await dbContext.TaskSlots.AsNoTracking().CountAsync(slot => slot.State != TaskSlotState.Available),
            Reserves = await dbContext.LedgerTransactions.AsNoTracking()
                .CountAsync(transaction => transaction.Kind == "reserve"),
            Entries = await dbContext.LedgerEntries.AsNoTracking().ToListAsync(),
        });

        Assert.Single(committed.Leases);
        Assert.Equal(1, committed.Live);
        Assert.Equal(1, committed.Leased);
        Assert.Equal(1, committed.Reserves);
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));
    }

    [Fact]
    public async Task PART1BoundaryAMemberOfTheProjectCannotApplyToEarnFromIt()
    {
        var task = await PublishTaskAsync(slotCount: 1);

        // The owner adds bob as a viewer of the project of the task.
        await _host.SignInAsync("alice");

        var member = await _host.SendAsync(
            HttpMethod.Put,
            $"/v1/projects/{task.ProjectId}/members/bob",
            "{\"role\":\"viewer\"}");

        Assert.Equal(HttpStatusCode.OK, member.StatusCode);

        // Bob offers the matching capability.
        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await ApplyAsync("bob", task.TaskId, await VersionAsync(task.TaskId));

        Assert.Equal(HttpStatusCode.Forbidden, applied.StatusCode);

        var error = await ReadJsonAsync(applied);

        Assert.Equal("forbidden", error.RootElement.GetProperty("error").GetProperty("code").GetString());

        // No seat, no application and no escrow of the member.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participations = await dbContext.Participations.AsNoTracking().CountAsync(),
            Applications = await dbContext.TaskApplications.AsNoTracking().CountAsync(),
            Leases = await dbContext.SlotLeases.AsNoTracking().CountAsync(),
        });

        Assert.Equal(0, committed.Participations);
        Assert.Equal(0, committed.Applications);
        Assert.Equal(0, committed.Leases);
    }

    [Fact]
    public async Task PART1TheWorkOfTheContributorListsTheSeatsAndHidesTheSeatsOfOthers()
    {
        var task = await PublishTaskAsync(slotCount: 2);

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await ApplyAsync("bob", task.TaskId, await VersionAsync(task.TaskId));
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        var work = await _host.SendAsync(HttpMethod.Get, "/v1/work");

        Assert.Equal(HttpStatusCode.OK, work.StatusCode);

        var list = await ReadJsonAsync(work);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Single(items);
        Assert.Equal("bob", items[0].GetProperty("contributor_username").GetString());

        // Another contributor never reads the seat of bob.
        await _host.SignInAsync("dana");

        var other = await _host.SendAsync(HttpMethod.Get, "/v1/work");

        Assert.Equal(HttpStatusCode.OK, other.StatusCode);

        var otherList = await ReadJsonAsync(other);

        Assert.Equal(0, otherList.RootElement.GetProperty("items").GetArrayLength());
    }

    /// <summary>Publishes a task of the owner with one region requirement.</summary>
    private async Task<PublishedTask> PublishTaskAsync(int slotCount)
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            ParticipationTestHost.TaskBody(
                projectId,
                slotCount: slotCount,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the task page\",\"expected_result\":\"The page shows the seat.\"}"],
                deadline: ParticipationTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = (await ReadJsonAsync(created)).RootElement.GetProperty("id").GetGuid();

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"part1-publish-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        return new PublishedTask(taskId.ToString(), projectId);
    }

    /// <summary>Applies for a seat of the task as the signed in contributor.</summary>
    private async Task<HttpResponseMessage> ApplyAsync(string actor, string taskId, int version)
    {
        await _host.SignInAsync(actor);

        return await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{version}}}",
            idempotencyKey: $"part1-apply-{Guid.NewGuid():N}");
    }

    /// <summary>The version of the task that the contributor saw.</summary>
    private async Task<int> VersionAsync(string taskId) =>
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var task = await dbContext.Tasks.AsNoTracking()
                .FirstAsync(item => item.Id == Guid.Parse(taskId));

            return task.Version;
        });

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonDocument.Parse(body);
    }

    /// <summary>A published task of the owner and the project it belongs to.</summary>
    private sealed record PublishedTask(string TaskId, string ProjectId);

    /// <summary>
    /// Empties the tables of the tasks, of the participations and of the
    /// credits of the test run, so every test starts with an empty marketplace
    /// and an empty journal.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<Blindmesh.Infrastructure.Persistence.BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new Blindmesh.Infrastructure.Persistence.BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");
    }
}
