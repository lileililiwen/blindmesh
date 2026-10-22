using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Tasks;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Matching;

/// <summary>
/// MATCH-1 Reach: the matched scope means exactly that. A task of a matched
/// project and a matched task are readable by the accounts that satisfy every
/// hard condition, and by nobody else. A task that the list of the
/// marketplace does not show is not readable through the detail and not
/// through the eligibility, and a seat that was already taken is untouched
/// when the scope closes.
/// </summary>
[Collection(MatchingCollection.Name)]
public sealed class MATCH1ReachTests : IAsyncLifetime
{
    private const string Device = "MacBook Pro 14";

    private readonly RuntimeFixture _fixture;

    private MatchingTestHost _host = null!;

    public MATCH1ReachTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new MatchingTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MATCH1SuccessTheCandidateThatFitsTheConditionsSeesTheTaskOfTheMatchedProject()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Matched project", "matched");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        // Bob offers the device that the task asks for.
        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        // The task of the matched project appears in the list of Bob.
        var list = await _host.SendAsync(HttpMethod.Get, "/v1/tasks");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var listed = await ReadTaskIdsAsync(list);

        Assert.Contains(taskId, listed);

        // The detail of the task is readable for Bob.
        var detail = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        using var detailDocument = await _host.ReadJsonAsync(detail);

        Assert.Equal(taskId, detailDocument.RootElement.GetProperty("id").GetString());

        // The eligibility names the condition of the task.
        var eligibility = await _host.SendAsync(
            HttpMethod.Get,
            $"/v1/capabilities/tasks/{taskId}/eligibility");

        Assert.Equal(HttpStatusCode.OK, eligibility.StatusCode);

        using var eligibilityDocument = await _host.ReadJsonAsync(eligibility);

        Assert.True(eligibilityDocument.RootElement.GetProperty("eligible").GetBoolean());

        // Bob may apply for a seat of the task.
        var applied = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{await _host.VersionAsync(taskId)}}}",
            idempotencyKey: $"match1-success-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
    }

    [Fact]
    public async Task MATCH1RejectionTheCandidateThatDoesNotFitNeverSeesTheTask()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Matched project", "matched");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        // Bob offers another device, so he fits no condition of the task.
        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", "Surface Laptop", ["surface"]);

        var list = await _host.SendAsync(HttpMethod.Get, "/v1/tasks");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var listed = await ReadTaskIdsAsync(list);

        Assert.DoesNotContain(taskId, listed);

        // The task is not found for Bob.
        var detail = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // The eligibility names no condition of the task.
        var eligibility = await _host.SendAsync(
            HttpMethod.Get,
            $"/v1/capabilities/tasks/{taskId}/eligibility");

        Assert.Equal(HttpStatusCode.NotFound, eligibility.StatusCode);

        var body = await eligibility.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain(Device, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("condition", body, StringComparison.OrdinalIgnoreCase);

        // Bob may not apply for a seat of a task he may not read.
        var applied = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{await _host.VersionAsync(taskId)}}}",
            idempotencyKey: $"match1-rejection-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, applied.StatusCode);
    }

    [Fact]
    public async Task MATCH1BoundaryTheReachEndsWithTheScopeAndKeepsTheTakenSeat()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Matched project", "matched");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        // Bob takes a seat of the task.
        var applied = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{await _host.VersionAsync(taskId)}}}",
            idempotencyKey: $"match1-boundary-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        using var application = await _host.ReadJsonAsync(applied);
        var participationId = application.RootElement.GetProperty("id").GetGuid();

        await _host.SignInAsync("alice");

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"match1-boundary-accept-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using var seat = await _host.ReadJsonAsync(accepted);

        Assert.Equal("accepted", seat.RootElement.GetProperty("state").GetString());
        Assert.Equal("leased", seat.RootElement.GetProperty("slot_state").GetString());

        // The project leaves the matched scope and becomes private.
        var projectVersion = await _host.WithDatabaseAsync(async dbContext =>
        {
            var project = await dbContext.Projects.AsNoTracking()
                .FirstAsync(item => item.Id == Guid.Parse(projectId));

            return project.Version;
        });

        var closed = await _host.SendAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            $"{{\"visibility\":\"private\",\"expected_version\":{projectVersion}}}");

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        // The reach ends at once for Bob.
        await _host.SignInAsync("bob");

        var list = await _host.SendAsync(HttpMethod.Get, "/v1/tasks");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var listed = await ReadTaskIdsAsync(list);

        Assert.DoesNotContain(taskId, listed);

        var detail = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // The seat that was already taken is untouched.
        var participation = await _host.WithDatabaseAsync(async dbContext =>
        {
            var row = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == participationId);

            return new { row.State, row.ContributorId };
        });

        Assert.Equal(ParticipationState.Accepted, participation.State);
        Assert.Equal(await _host.UserIdAsync("bob"), participation.ContributorId);

        var lease = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.SlotLeases.AsNoTracking()
                .AnyAsync(item => item.ParticipationId == participationId));

        Assert.True(lease);
    }

    private async Task<string[]> ReadTaskIdsAsync(HttpResponseMessage response)
    {
        using var document = await _host.ReadJsonAsync(response);

        return document.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToArray();
    }

    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<Blindmesh.Infrastructure.Persistence.BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new Blindmesh.Infrastructure.Persistence.BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history, reputation_events, reputation_scores, reputation_snapshots CASCADE");
    }
}
