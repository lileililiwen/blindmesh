using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Tasks;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// PART-2 Deadlines: an accepted contributor that starts in time sees the
/// visible deadline of the architecture and the task status is recomputed. A
/// sweep of the deadlines that races a start command produces one legal
/// transition and one financial outcome. A cancellation of a task with running
/// work closes the admission and keeps the escrow and the submission grace of
/// the running work.
/// </summary>
[Collection(ParticipationCollection.Name)]
public sealed class PART2DeadlineTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ParticipationTestHost _host = null!;

    public PART2DeadlineTests(RuntimeFixture fixture)
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
    public async Task PART2SuccessTheVisibleDeadlineIsTheSmallerOfTheTaskDeadlineAndSevenDays()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 30);

        var participationId = await AcceptedSeatAsync(task);

        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: "part2-success-start-0001");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var seat = await ReadJsonAsync(started);
        var deadline = seat.RootElement.GetProperty("execution_deadline").GetDateTimeOffset();

        // The task deadline lies further away than seven days, so the visible
        // deadline is the start plus seven days.
        Assert.Equal(7, Math.Round((deadline - seat.RootElement.GetProperty("started_at").GetDateTimeOffset()).TotalDays));

        // The aggregate status of the task is recomputed.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Task = await dbContext.Tasks.AsNoTracking().FirstAsync(item => item.Id == Guid.Parse(task.TaskId)),
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == participationId),
            History = await dbContext.TaskStatusChanges.AsNoTracking()
                .Where(change => change.TaskId == Guid.Parse(task.TaskId))
                .ToListAsync(),
        });

        Assert.Equal(TaskState.InProgress, committed.Task.Status);
        Assert.Equal(ParticipationState.Running, committed.Participation.State);

        var transition = committed.History.Last();
        Assert.Equal("open", transition.FromStatus);
        Assert.Equal("in_progress", transition.ToStatus);
    }

    [Fact]
    public async Task PART2SuccessTheTaskDeadlineBoundsTheVisibleDeadline()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 2);

        var participationId = await AcceptedSeatAsync(task);

        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: "part2-success-short-0001");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var seat = await ReadJsonAsync(started);

        var execution = seat.RootElement.GetProperty("execution_deadline").GetDateTimeOffset();
        var taskDeadline = await _host.WithDatabaseAsync(async dbContext =>
        {
            var row = await dbContext.Tasks.AsNoTracking().FirstAsync(item => item.Id == Guid.Parse(task.TaskId));

            return row.Deadline!.Value;
        });

        Assert.Equal(taskDeadline, execution);
    }

    [Fact]
    public async Task PART2RejectionASweepThatRacesAStartCommandProducesOneLegalTransition()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 30);

        var participationId = await AcceptedSeatAsync(task);

        // The start window of the seat passed before the sweep runs.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var participation = await dbContext.Participations.FirstAsync(item => item.Id == participationId);
            participation.AcceptedAt = DateTimeOffset.UtcNow.AddHours(-25);
            await dbContext.SaveChangesAsync();
        });

        var sweep = await _host.RunDeadlineSweepAsync();

        Assert.Equal(1, sweep.Expired);

        // The start of the expired seat is refused.
        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: "part2-rejection-start-0001");

        Assert.Equal(HttpStatusCode.Conflict, started.StatusCode);

        var error = await ReadJsonAsync(started);

        Assert.Equal("invalid_transition", error.RootElement.GetProperty("error").GetProperty("code").GetString());

        // The seat expired exactly once, the slot is free again and the escrow
        // of the publisher holds the whole budget.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == participationId),
            Slot = await dbContext.TaskSlots.AsNoTracking().FirstAsync(item => item.TaskId == Guid.Parse(task.TaskId)),
            Leases = await dbContext.SlotLeases.AsNoTracking().ToListAsync(),
            History = await dbContext.ParticipationStatusChanges.AsNoTracking()
                .Where(change => change.ParticipationId == participationId)
                .ToListAsync(),
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Pending))
                .CountAsync(),
            Reserve = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Reserved))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
        });

        Assert.Equal(ParticipationState.Expired, committed.Participation.State);
        Assert.Equal(TaskSlotState.Available, committed.Slot.State);
        Assert.Single(committed.Leases);
        Assert.NotNull(committed.Leases[0].ReleasedAt);
        Assert.Equal(15, committed.Reserve);
        Assert.Equal(0, committed.Pending);
        Assert.Equal(
            new[] { "applied", "accepted", "expired" },
            committed.History.Select(change => change.ToStatus).ToArray());

    }

    [Fact]
    public async Task PART2RejectionTheStartOfASeatAfterItsWindowIsRefused()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 30);

        var participationId = await AcceptedSeatAsync(task);

        await _host.WithDatabaseAsync(async dbContext =>
        {
            var participation = await dbContext.Participations.FirstAsync(item => item.Id == participationId);
            participation.AcceptedAt = DateTimeOffset.UtcNow.AddHours(-25);
            await dbContext.SaveChangesAsync();
        });

        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: "part2-rejection-late-0001");

        Assert.Equal(HttpStatusCode.Conflict, started.StatusCode);

        // The seat is still accepted and the escrow never moved.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == participationId),
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Pending))
                .CountAsync(),
        });

        Assert.Equal(ParticipationState.Accepted, committed.Participation.State);
        Assert.Equal(0, committed.Pending);
    }

    [Fact]
    public async Task PART2BoundaryTheCancellationOfATaskWithRunningWorkKeepsTheEscrowAndTheGrace()
    {
        var task = await PublishTaskAsync(slotCount: 2, deadlineInDays: 30);

        var participationId = await AcceptedSeatAsync(task);

        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: "part2-boundary-start-0001");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var seat = await ReadJsonAsync(started);
        var executionDeadline = seat.RootElement.GetProperty("execution_deadline").GetDateTimeOffset();

        await _host.SignInAsync("alice");

        var cancelled = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{task.TaskId}/cancel",
            idempotencyKey: "part2-boundary-cancel-0001");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        // The admission is closed and the unused slot is refunded.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Task = await dbContext.Tasks.AsNoTracking().FirstAsync(item => item.Id == Guid.Parse(task.TaskId)),
            Slots = await dbContext.TaskSlots.AsNoTracking()
                .Where(slot => slot.TaskId == Guid.Parse(task.TaskId)).ToListAsync(),
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == participationId),
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Pending))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Reserve = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Reserved))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
        });

        Assert.Equal(TaskState.Cancelled, committed.Task.Status);
        Assert.Equal(ParticipationState.Running, committed.Participation.State);
        Assert.True(
            Math.Abs((executionDeadline - committed.Participation.ExecutionDeadline!.Value).TotalSeconds) < 1,
            "the running work keeps the submission grace of its visible deadline.");

        var active = Assert.Single(committed.Slots, slot => slot.State == TaskSlotState.Active);
        Assert.NotNull(active.StartedAt);

        var refunded = Assert.Single(committed.Slots, slot => slot.State == TaskSlotState.Refunded);

        Assert.Equal(15, refunded.Reward);

        // The escrow of the running work keeps its budget.
        Assert.Equal(10, committed.Pending);
        Assert.Equal(5, committed.Reserve);
    }

    [Fact]
    public async Task PART2BoundaryTheWithdrawalOfASeatGivesTheSlotBackToTheTask()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 30);

        var participationId = await AcceptedSeatAsync(task);

        await _host.SignInAsync("bob");

        var withdrawn = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/withdraw",
            idempotencyKey: "part2-boundary-withdraw-0001");

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);

        var seat = await ReadJsonAsync(withdrawn);

        Assert.Equal("withdrawn", seat.RootElement.GetProperty("state").GetString());

        // The slot is free again and the budget stays reserved for the next
        // contributor of the task.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Slot = await dbContext.TaskSlots.AsNoTracking().FirstAsync(item => item.TaskId == Guid.Parse(task.TaskId)),
            Lease = await dbContext.SlotLeases.AsNoTracking().FirstAsync(),
            Reserve = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Reserved))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Pending))
                .CountAsync(),
        });

        Assert.Equal(TaskSlotState.Available, committed.Slot.State);
        Assert.Null(committed.Slot.ContributorId);
        Assert.NotNull(committed.Lease.ReleasedAt);
        Assert.Equal(15, committed.Reserve);
        Assert.Equal(0, committed.Pending);
    }

    [Fact]
    public async Task PART2TheApplicationOfAContributorIsAcceptedOnlyOnce()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 30);

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var version = await VersionAsync(task.TaskId);

        var first = await ApplyAsync("bob", task.TaskId, version, "part2-apply-first-0001");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await ApplyAsync("bob", task.TaskId, version, "part2-apply-second-0001");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var error = await ReadJsonAsync(second);

        Assert.Equal("state_conflict", error.RootElement.GetProperty("error").GetProperty("code").GetString());

        // The second application is refused, so the seat stays unique.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participations = await dbContext.Participations.AsNoTracking().CountAsync(),
            Applications = await dbContext.TaskApplications.AsNoTracking().CountAsync(),
        });

        Assert.Equal(1, committed.Participations);
        Assert.Equal(1, committed.Applications);
    }

    [Fact]
    public async Task PART2AnApplicationAfterTheAdmissionClosedIsRefused()
    {
        var task = await PublishTaskAsync(slotCount: 1, deadlineInDays: 30);

        // The publication lies more than 48 hours in the past.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var row = await dbContext.Tasks.FirstAsync(item => item.Id == Guid.Parse(task.TaskId));
            row.PublishedAt = DateTimeOffset.UtcNow.AddHours(-49);
            await dbContext.SaveChangesAsync();
        });

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await ApplyAsync("bob", task.TaskId, await VersionAsync(task.TaskId), "part2-apply-late-0001");

        Assert.Equal(HttpStatusCode.Conflict, applied.StatusCode);

        var error = await ReadJsonAsync(applied);

        Assert.Equal("state_conflict", error.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>Publishes a task of the owner with one region requirement.</summary>
    private async Task<PublishedTask> PublishTaskAsync(int slotCount, int deadlineInDays)
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
                deadline: ParticipationTestHost.DeadlineInDays(deadlineInDays)));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = (await ReadJsonAsync(created)).RootElement.GetProperty("id").GetGuid();

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"part2-publish-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        return new PublishedTask(taskId.ToString(), projectId);
    }

    /// <summary>Applies for a seat and lets the owner accept it.</summary>
    private async Task<Guid> AcceptedSeatAsync(PublishedTask task)
    {
        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await ApplyAsync(
            "bob",
            task.TaskId,
            await VersionAsync(task.TaskId),
            $"part2-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        var participationId = (await ReadJsonAsync(applied)).RootElement.GetProperty("id").GetGuid();

        await _host.SignInAsync("alice");

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"part2-accept-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        return participationId;
    }

    /// <summary>Applies for a seat of the task as the signed in contributor.</summary>
    private async Task<HttpResponseMessage> ApplyAsync(string actor, string taskId, int version, string key)
    {
        await _host.SignInAsync(actor);

        return await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{version}}}",
            idempotencyKey: key);
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
