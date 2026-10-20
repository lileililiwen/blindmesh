using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Tasks;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// TASK-1 Publication: an authorized owner publishes a valid task and the whole
/// base-plus-bonus budget of the escrow is reserved before the task is open.
/// A task without the required acceptance rules or without the credits is
/// refused and leaves neither an open task nor a partial reservation behind.
/// A publish that is retried with the same key returns the same task and the
/// same reservation exactly once.
/// </summary>
[Collection(TaskCollection.Name)]
public sealed class TASK1PublicationTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private TaskTestHost _host = null!;

    public TASK1PublicationTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new TaskTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TASK1SuccessAPublicationReservesTheWholeBudgetAndOpensTheTask()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                modes: ["verify", "explore"],
                acceptsCleanFindings: true,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = await ReadIdAsync(created);
        var draft = await ReadJsonAsync(created);

        Assert.Equal("draft", draft.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, draft.RootElement.GetProperty("reserved_budget").GetInt64());

        // The publication reserves the base reward and the bonus of every slot.
        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: "task1-success-publish-0001");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var detail = await ReadJsonAsync(published);

        Assert.Equal("open", detail.RootElement.GetProperty("status").GetString());
        Assert.Equal(taskId, detail.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(30, detail.RootElement.GetProperty("maximum_budget").GetInt64());
        Assert.Equal(30, detail.RootElement.GetProperty("reserved_budget").GetInt64());
        Assert.Equal(2, detail.RootElement.GetProperty("slots").GetArrayLength());

        // The task and the reservation are committed together.
        var committed = await _host.WithDatabaseAsync(async dbContext =>
        {
            var task = await dbContext.Tasks.AsNoTracking().FirstAsync(item => item.Id == taskId);

            var slots = await dbContext.TaskSlots.AsNoTracking().Where(slot => slot.TaskId == taskId).ToListAsync();

            var history = await dbContext.TaskStatusChanges.AsNoTracking()
                .Where(change => change.TaskId == taskId)
                .ToListAsync();

            var reservedAccounts = dbContext.LedgerAccounts
                .Where(account => account.OwnerId == task.OwnerId && account.Bucket == LedgerBucket.Reserved)
                .Select(account => account.Id);

            var reserved = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => reservedAccounts.Contains(entry.AccountId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0;

            return (task, slots, history, reserved);
        });

        Assert.Equal(TaskState.Open, committed.task.Status);
        Assert.Equal(30, committed.task.ReservedBudget);
        Assert.NotNull(committed.task.PublishedAt);
        Assert.Equal(2, committed.slots.Count);
        Assert.All(committed.slots, slot => Assert.Equal(15, slot.Reward));
        Assert.All(committed.slots, slot => Assert.Equal(TaskSlotState.Available, slot.State));

        // The history of the task holds the applied transition.
        var transition = Assert.Single(committed.history);
        Assert.Equal("draft", transition.FromStatus);
        Assert.Equal("open", transition.ToStatus);

        // The escrow of the publisher holds the whole budget.
        Assert.Equal(30, committed.reserved);

        // The journal of the credits stays balanced.
        var sums = await _host.WithDatabaseAsync(async dbContext => await dbContext.LedgerEntries
            .AsNoTracking()
            .ToListAsync());

        Assert.Equal(4, sums.Count);

        var byTransaction = sums.GroupBy(entry => entry.TransactionId).ToList();

        Assert.Equal(2, byTransaction.Count);
        Assert.All(byTransaction, transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));
    }

    [Fact]
    public async Task TASK1RejectionATaskWithoutTheRequiredStepsIsRefusedAndLeavesNoReservation()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                modes: ["verify"],
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: null,
                deadline: TaskTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = await ReadIdAsync(created);

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: "task1-rejection-steps-0001");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, published.StatusCode);

        var error = await ReadJsonAsync(published);

        Assert.Equal("unprocessable_entity", error.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.True(
            error.RootElement.GetProperty("error").GetProperty("field_errors").TryGetProperty("steps", out _),
            "the refusal names the field of the missing steps.");

        // No open task and no entry of the journal.
        var state = await _host.WithDatabaseAsync(async dbContext => new
        {
            Open = await dbContext.Tasks.CountAsync(task => task.Status == TaskState.Open),
            Draft = await dbContext.Tasks.CountAsync(task => task.Status == TaskState.Draft),
            Reserves = await dbContext.LedgerTransactions.CountAsync(transaction => transaction.Kind == "reserve"),
            Entries = await dbContext.LedgerEntries.CountAsync(),
            Slots = await dbContext.TaskSlots.CountAsync(),
        });

        Assert.Equal(0, state.Open);
        Assert.Equal(1, state.Draft);
        Assert.Equal(0, state.Reserves);
        Assert.Equal(0, state.Slots);
    }

    [Fact]
    public async Task TASK1RejectionAPublisherWithoutTheCreditsIsRefusedAndLeavesNoReservation()
    {
        await _host.SignInAsync("bob");

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                modes: ["verify"],
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = await ReadIdAsync(created);

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: "task1-rejection-funds-0001");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, published.StatusCode);

        var error = await ReadJsonAsync(published);

        Assert.Equal("insufficient_funds", error.RootElement.GetProperty("error").GetProperty("code").GetString());

        // The task stays a draft and the journal holds no entry.
        var state = await _host.WithDatabaseAsync(async dbContext => new
        {
            Open = await dbContext.Tasks.CountAsync(task => task.Status == TaskState.Open),
            Reserves = await dbContext.LedgerTransactions.CountAsync(transaction => transaction.Kind == "reserve"),
            Entries = await dbContext.LedgerEntries.CountAsync(),
            Slots = await dbContext.TaskSlots.CountAsync(),
        });

        Assert.Equal(0, state.Open);
        Assert.Equal(0, state.Reserves);
        Assert.Equal(0, state.Entries);
        Assert.Equal(0, state.Slots);
    }

    [Fact]
    public async Task TASK1BoundaryARetriedPublicationReturnsTheSameTaskAndTheSameReservation()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                modes: ["verify"],
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        var taskId = await ReadIdAsync(created);

        var first = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: "task1-boundary-publish-0001");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The response of the first publication is lost and the caller retries
        // the command with the same key.
        var retry = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: "task1-boundary-publish-0001");

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var firstBody = await first.Content.ReadAsStringAsync(CancellationToken.None);
        var retryBody = await retry.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(firstBody, retryBody);

        // The reservation happened exactly once.
        var state = await _host.WithDatabaseAsync(async dbContext => new
        {
            Open = await dbContext.Tasks.CountAsync(task => task.Status == TaskState.Open),
            Transactions = await dbContext.LedgerTransactions
                .CountAsync(transaction => transaction.Kind == "reserve"),
            Entries = await dbContext.LedgerEntries.CountAsync(entry => dbContext.LedgerTransactions
                .Any(transaction => transaction.Id == entry.TransactionId && transaction.Kind == "reserve")),
            Slots = await dbContext.TaskSlots.CountAsync(),
        });

        Assert.Equal(1, state.Open);
        Assert.Equal(1, state.Transactions);
        Assert.Equal(2, state.Entries);
        Assert.Equal(2, state.Slots);
    }

    [Fact]
    public async Task TASK1TheDraftOfATaskIsChangedWithTheVersionOfTheTask()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = await ReadIdAsync(created);
        var draft = await ReadJsonAsync(created);
        var version = draft.RootElement.GetProperty("version").GetInt32();

        var changed = await _host.SendAsync(
            HttpMethod.Patch,
            $"/v1/tasks/{taskId}",
            TaskTestHost.TaskBody(
                projectId,
                title: "A changed title of the task",
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays())
                .Replace("\"slot_count\":2,", $"\"slot_count\":2,\"expected_version\":{version},"));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var updated = await ReadJsonAsync(changed);

        Assert.Equal("A changed title of the task", updated.RootElement.GetProperty("title").GetString());
        Assert.Equal(version + 1, updated.RootElement.GetProperty("version").GetInt32());

        // A stale version is refused with the current one.
        var stale = await _host.SendAsync(
            HttpMethod.Patch,
            $"/v1/tasks/{taskId}",
            TaskTestHost.TaskBody(
                projectId,
                title: "A title of a stale change",
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays())
                .Replace("\"slot_count\":2,", $"\"slot_count\":2,\"expected_version\":{version},"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var error = await ReadJsonAsync(stale);

        Assert.Equal("version_conflict", error.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TASK1TheSameKeyWithAnotherTaskIsRefused()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var first = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        var second = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                title: "A second task of the marketplace",
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        var firstId = await ReadIdAsync(first);
        var secondId = await ReadIdAsync(second);

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{firstId}/publish",
            idempotencyKey: "task1-boundary-reuse-0001");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var reused = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{secondId}/publish",
            idempotencyKey: "task1-boundary-reuse-0001");

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

        var error = await ReadJsonAsync(reused);

        Assert.Equal("idempotency_key_reuse", error.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var document = await TaskTestHostJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await TaskTestHostJsonAsync(response);

    private static async Task<JsonDocument> TaskTestHostJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Empties the tables of the tasks and of the credits of the test run, so
    /// every test starts with an empty marketplace and an empty journal.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<Blindmesh.Infrastructure.Persistence.BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new Blindmesh.Infrastructure.Persistence.BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records CASCADE");
    }
}
