using System.Net;
using System.Text.Json;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Matching;

/// <summary>
/// MATCH-2 Candidates: the owner of a project reads the accounts that fit a
/// task with the declaration that satisfied every condition. Every private
/// field of an account and of a declaration stays out of the answer, and an
/// account outside the project learns nothing.
/// </summary>
[Collection(MatchingCollection.Name)]
public sealed class MATCH2CandidateTests : IAsyncLifetime
{
    private const string Device = "MacBook Pro 14";

    private readonly RuntimeFixture _fixture;

    private MatchingTestHost _host = null!;

    public MATCH2CandidateTests(RuntimeFixture fixture)
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
    public async Task MATCH2SuccessTheOwnerReadsTheCandidatesWithTheConditionThatEachDeclarationSatisfied()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Candidate project", "public");

        // The condition asks for the tag of the declaration, so the answer
        // names the label and the tag that matched.
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", "macbook"));

        // Bob offers the device with a tag that matches the condition.
        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", "Work laptop", ["macbook", "work"]);

        await _host.SignInAsync("alice");

        var response = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await _host.ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(taskId, root.GetProperty("task_id").GetString());

        // The condition of the task is named.
        var conditions = root.GetProperty("conditions").EnumerateArray().ToArray();

        Assert.Single(conditions);
        Assert.Equal("device", conditions[0].GetProperty("kind").GetString());
        Assert.Equal("macbook", conditions[0].GetProperty("value").GetString());

        // Every account that fits is listed with the declaration that
        // satisfied the condition.
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Single(candidates);

        var candidate = candidates[0];

        Assert.Equal("bob", candidate.GetProperty("username").GetString());
        Assert.Equal("/people/bob", candidate.GetProperty("profile_reference").GetString());
        Assert.False(string.IsNullOrWhiteSpace(candidate.GetProperty("display_name").GetString()));

        var outcomes = candidate.GetProperty("conditions").EnumerateArray().ToArray();

        Assert.Single(outcomes);
        Assert.Equal("device", outcomes[0].GetProperty("kind").GetString());
        Assert.Equal("macbook", outcomes[0].GetProperty("value").GetString());
        Assert.True(outcomes[0].GetProperty("satisfied").GetBoolean());
        Assert.Equal("Work laptop", outcomes[0].GetProperty("declaration_label").GetString());
        Assert.Equal("macbook", outcomes[0].GetProperty("declaration_tag").GetString());

        // The answer holds no private field of an account and no private detail
        // of a declaration.
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bio", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@blindmesh.test", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MATCH2RejectionTheAccountOutsideTheProjectLearnsNothing()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Candidate project", "public");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        // Erin is not a member of the project of the task.
        await _host.SignInAsync("erin");

        var response = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // The refusal holds neither a candidate nor a condition.
        Assert.DoesNotContain("bob", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Device, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accepted_contributions", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profile_reference", body, StringComparison.OrdinalIgnoreCase);

        // A visitor without a session is refused as well.
        var anonymous = await _host.CreateClientWithoutCookies()
            .GetAsync($"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task MATCH2BoundaryTheWithdrawnOrPrivateDeclarationLeavesTheListAndNothingPrivateIsShown()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Candidate project", "public");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        // Bob offers the device, but the declaration is private, so it never
        // reaches a match.
        await _host.SignInAsync("bob");
        var privateDeclaration = await _host.DeclareCapabilityAsync("device", "Private laptop", ["macbook"], "private");

        await _host.SignInAsync("alice");

        var before = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var beforeDocument = await _host.ReadJsonAsync(before))
        {
            Assert.Empty(beforeDocument.RootElement.GetProperty("candidates").EnumerateArray());
        }

        // Bob offers the device again, this time in public.
        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        await _host.SignInAsync("alice");

        var listed = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        using (var listedDocument = await _host.ReadJsonAsync(listed))
        {
            var candidates = listedDocument.RootElement.GetProperty("candidates").EnumerateArray().ToArray();

            Assert.Single(candidates);
            Assert.Equal("bob", candidates[0].GetProperty("username").GetString());
        }

        // Bob withdraws the public declaration: the account leaves the list.
        await _host.SignInAsync("bob");

        var publicDeclaration = await _host.FindMatchableDeclarationAsync("bob", Device);

        Assert.NotNull(publicDeclaration);

        var withdrawn = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/capabilities/{publicDeclaration!.Value.Id}/withdraw",
            $"{{\"expected_version\":{publicDeclaration.Value.Version}}}",
            idempotencyKey: $"match2-boundary-withdraw-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);

        await _host.SignInAsync("alice");

        var after = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        using var afterDocument = await _host.ReadJsonAsync(after);

        Assert.Empty(afterDocument.RootElement.GetProperty("candidates").EnumerateArray());

        // The private declaration of Bob never appears in the answer.
        var body = await after.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("Private laptop", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privateDeclaration, body, StringComparison.OrdinalIgnoreCase);
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
