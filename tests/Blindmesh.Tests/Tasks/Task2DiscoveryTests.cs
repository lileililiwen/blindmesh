using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Tasks;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// TASK-2 Discovery: the marketplace lists the public tasks with a bounded
/// cursor and filters them by mode and by the hard requirements of the
/// contributor. The summary of a candidate never carries the scope or the
/// instructions of an access. A task that the visitor may not see is not found
/// and never leaks the project or the contributor. A cancellation closes the
/// admission and refunds the unused slots, while the running work keeps its
/// budget.
/// </summary>
[Collection(TaskCollection.Name)]
public sealed class TASK2DiscoveryTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private TaskTestHost _host = null!;

    public TASK2DiscoveryTests(RuntimeFixture fixture)
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
    public async Task TASK2SuccessAVisitorFiltersThePublicTasksByModeAndRequirement()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var verifyId = await CreatePublishedTaskAsync(
            projectId,
            title: "Verify the sign in",
            modes: ["verify"],
            requirement: "{\"kind\":\"region\",\"value\":\"eu-central\"}",
            step: "{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}");

        await CreatePublishedTaskAsync(
            projectId,
            title: "Explore the wallet",
            modes: ["explore"],
            requirement: "{\"kind\":\"device\",\"value\":\"laptop\"}",
            step: null,
            acceptsCleanFindings: true);

        await CreatePublishedTaskAsync(
            projectId,
            title: "Review the report",
            modes: ["review"],
            requirement: "{\"kind\":\"region\",\"value\":\"eu-central\"}",
            step: null,
            reviewCriteria: "Rate the clarity, the evidence and the reproducibility of the report.");

        var visitor = _host.CreateClientWithoutCookies();

        // The visitor sees the public tasks of an active public project.
        var all = await visitor.GetAsync("/v1/tasks", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, all.StatusCode);

        var allBody = await all.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains("Verify the sign in", allBody, StringComparison.Ordinal);
        Assert.Contains("Explore the wallet", allBody, StringComparison.Ordinal);
        Assert.Contains("Review the report", allBody, StringComparison.Ordinal);

        // The summary of a candidate never carries the scope of the task.
        Assert.DoesNotContain("Walk through the sign in", allBody, StringComparison.Ordinal);

        // The filter of the mode keeps only the tasks of that mode.
        var verify = await visitor.GetAsync("/v1/tasks?mode=verify", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var verifyBody = await verify.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains("Verify the sign in", verifyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Explore the wallet", verifyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Review the report", verifyBody, StringComparison.Ordinal);

        // The filter of the hard requirement keeps only the tasks that ask for
        // the region of the contributor.
        var region = await visitor.GetAsync("/v1/tasks?requirement=region:eu-central", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, region.StatusCode);

        var regionBody = await region.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains("Verify the sign in", regionBody, StringComparison.Ordinal);
        Assert.Contains("Review the report", regionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Explore the wallet", regionBody, StringComparison.Ordinal);

        var device = await visitor.GetAsync("/v1/tasks?requirement=device:laptop", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, device.StatusCode);

        var deviceBody = await device.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains("Explore the wallet", deviceBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify the sign in", deviceBody, StringComparison.Ordinal);

        // The pages of the cursor are stable and hold every task exactly once.
        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 5; page++)
        {
            var path = cursor is null ? "/v1/tasks?limit=1" : $"/v1/tasks?limit=1&cursor={cursor}";
            var response = await visitor.GetAsync(path, CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var document = await ReadJsonAsync(response);
            var items = document.RootElement.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                seen.Add(item.GetProperty("id").GetGuid());
            }

            cursor = document.RootElement.TryGetProperty("next_cursor", out var next) &&
                next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;

            if (cursor is null)
            {
                break;
            }
        }

        Assert.Equal(3, seen.Count);
        Assert.Equal(3, seen.Distinct().Count());
        Assert.Contains(verifyId, seen);
    }

    [Fact]
    public async Task TASK2RejectionAPrivateTaskIsNotFoundAndLeaksNothing()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("A private project of alice", "private");

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                title: "A private task of alice",
                visibility: "private",
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = await ReadIdAsync(created);

        var visitor = _host.CreateClientWithoutCookies();

        var response = await visitor.GetAsync($"/v1/tasks/{taskId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // The answer never names the project or the contributor.
        Assert.DoesNotContain("A private project of alice", body, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", body, StringComparison.Ordinal);
        Assert.DoesNotContain("A private task of alice", body, StringComparison.Ordinal);
        Assert.DoesNotContain(taskId.ToString(), body, StringComparison.Ordinal);

        // The task is not in the list of the marketplace either.
        var list = await visitor.GetAsync("/v1/tasks", CancellationToken.None);
        var listBody = await list.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain(taskId.ToString(), listBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TASK2BoundaryACancellationClosesTheAdmissionAndRefundsTheUnusedSlots()
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
                baseReward: 10,
                slotBonus: 5,
                slotCount: 3,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        var taskId = await ReadIdAsync(created);

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: "task2-boundary-publish-0001");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        // One slot carries running work. The participation package owns that
        // state, so the fixture writes it directly.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var slot = await dbContext.TaskSlots
                .Where(item => item.TaskId == taskId)
                .OrderBy(item => item.Position)
                .FirstAsync();

            slot.State = TaskSlotState.Active;
            slot.ContributorId = Guid.NewGuid();
            slot.StartedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();
        });

        var cancelled = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/cancel",
            idempotencyKey: "task2-boundary-cancel-0001");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var detail = await ReadJsonAsync(cancelled);

        Assert.Equal("cancelled", detail.RootElement.GetProperty("status").GetString());

        // The admission is closed and the unused slots are refunded, while the
        // running slot keeps its budget.
        var state = await _host.WithDatabaseAsync(async dbContext =>
        {
            var slots = await dbContext.TaskSlots.AsNoTracking().Where(slot => slot.TaskId == taskId).ToListAsync();

            var ownerId = await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.OwnerId)
                .FirstAsync();

            var reservedAccounts = dbContext.LedgerAccounts
                .Where(account => account.OwnerId == ownerId && account.Bucket == LedgerBucket.Reserved)
                .Select(account => account.Id);

            var availableAccounts = dbContext.LedgerAccounts
                .Where(account => account.OwnerId == ownerId && account.Bucket == LedgerBucket.UserAvailable)
                .Select(account => account.Id);

            var reserved = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => reservedAccounts.Contains(entry.AccountId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0;

            var available = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => availableAccounts.Contains(entry.AccountId))
                .SumAsync(entry => (long?)entry.Amount) ?? 0;

            var entries = await dbContext.LedgerEntries.AsNoTracking().ToListAsync();

            return (slots, reserved, available, entries);
        });

        Assert.Equal(3, state.slots.Count);
        Assert.Single(state.slots, slot => slot.State == TaskSlotState.Active);
        Assert.Equal(2, state.slots.Count(slot => slot.State == TaskSlotState.Refunded));

        // Only the running slot keeps its part of the escrow.
        Assert.Equal(15, state.reserved);
        Assert.Equal(45, state.available);

        // The journal of the credits stays balanced.
        var byTransaction = state.entries.GroupBy(entry => entry.TransactionId).ToList();

        Assert.Equal(3, byTransaction.Count);
        Assert.All(byTransaction, transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));
    }

    [Fact]
    public async Task TASK2TheEligibilityOfAPublishedTaskAnswersWithTheConditionsOfThatTask()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var taskId = await CreatePublishedTaskAsync(
            projectId,
            title: "Verify the sign in",
            modes: ["verify"],
            requirement: "{\"kind\":\"region\",\"value\":\"eu-central\"}",
            step: "{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}");

        // The contributor declares the region that the task requires.
        await _host.SignInAsync("bob");

        var capability = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            "{\"kind\":\"region\",\"label\":\"eu-central\",\"tags\":[],\"visibility\":\"public\",\"available\":true}");

        Assert.Equal(HttpStatusCode.Created, capability.StatusCode);

        var eligibility = await _host.Client.GetAsync(
            $"/v1/capabilities/tasks/{taskId}/eligibility",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, eligibility.StatusCode);

        var document = await ReadJsonAsync(eligibility);
        var conditions = document.RootElement.GetProperty("conditions");

        Assert.Equal(1, conditions.GetArrayLength());
        Assert.Equal("region", conditions[0].GetProperty("kind").GetString());
        Assert.Equal("eu-central", conditions[0].GetProperty("value").GetString());
        Assert.True(conditions[0].GetProperty("satisfied").GetBoolean());
        Assert.True(document.RootElement.GetProperty("eligible").GetBoolean());
    }

    [Fact]
    public async Task TASK2TheEligibilityOfATaskThatTheActorMayNotSeeIsNotFound()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("A private project of alice", "private");

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                title: "A private task of alice",
                visibility: "private",
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the sign in page\",\"expected_result\":\"The page shows the form.\"}"],
                deadline: TaskTestHost.DeadlineInDays()));

        var taskId = await ReadIdAsync(created);

        await _host.SignInAsync("bob");

        var eligibility = await _host.Client.GetAsync(
            $"/v1/capabilities/tasks/{taskId}/eligibility",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, eligibility.StatusCode);

        var body = await eligibility.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("eu-central", body, StringComparison.Ordinal);
        Assert.DoesNotContain("A private task of alice", body, StringComparison.Ordinal);
    }

    private async Task<Guid> CreatePublishedTaskAsync(
        string projectId,
        string title,
        string[] modes,
        string requirement,
        string? step,
        bool acceptsCleanFindings = false,
        string? reviewCriteria = null)
    {
        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            TaskTestHost.TaskBody(
                projectId,
                title: title,
                modes: modes,
                baseReward: 5,
                slotBonus: 0,
                slotCount: 1,
                acceptsCleanFindings: acceptsCleanFindings,
                reviewCriteria: reviewCriteria,
                requirements: [requirement],
                steps: step is null ? null : [step],
                deadline: TaskTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = await ReadIdAsync(created);

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"task2-success-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        return taskId;
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonDocument.Parse(body);
    }

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
