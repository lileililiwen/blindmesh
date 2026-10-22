using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Reputation;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Matching;

/// <summary>
/// MATCH-3 Fairness: the list of the candidates puts an account without an
/// accepted contribution before an account with one, and it never claims a
/// quality, a score or a rank. The order is stable for the same state.
/// </summary>
[Collection(MatchingCollection.Name)]
public sealed class MATCH3FairnessTests : IAsyncLifetime
{
    private const string Device = "MacBook Pro 14";

    private readonly RuntimeFixture _fixture;

    private MatchingTestHost _host = null!;

    public MATCH3FairnessTests(RuntimeFixture fixture)
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
    public async Task MATCH3SuccessTheAccountWithoutAnAcceptedContributionComesFirstAndTheReasonIsNamed()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Fair project", "public");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        // Bob and Cara both fit the condition of the task.
        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        await _host.SignInAsync("cara");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        // Cara has three accepted contributions, Bob has none.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var cara = await dbContext.Users.AsNoTracking()
                .FirstAsync(item => item.Username == "cara");

            dbContext.ReputationScores.Add(new ReputationScore
            {
                UserId = cara.Id,
                AcceptedContributions = 3,
                ConfirmedFindings = 3,
                ReversedContributions = 0,
                CurrentScore = 30,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            await dbContext.SaveChangesAsync();
        });

        await _host.SignInAsync("alice");

        var response = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await _host.ReadJsonAsync(response);
        var candidates = document.RootElement.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal(2, candidates.Length);

        // The account without an accepted contribution comes first.
        Assert.Equal("bob", candidates[0].GetProperty("username").GetString());
        Assert.Equal(0, candidates[0].GetProperty("accepted_contributions").GetInt32());
        Assert.Equal("explorer", candidates[0].GetProperty("order_reason").GetProperty("code").GetString());
        Assert.Equal(
            "No accepted contribution yet",
            candidates[0].GetProperty("order_reason").GetProperty("text").GetString());

        // The account with an accepted contribution comes second and the reason
        // names it.
        Assert.Equal("cara", candidates[1].GetProperty("username").GetString());
        Assert.Equal(3, candidates[1].GetProperty("accepted_contributions").GetInt32());
        Assert.Equal("established", candidates[1].GetProperty("order_reason").GetProperty("code").GetString());
        Assert.Equal(
            "3 accepted contributions",
            candidates[1].GetProperty("order_reason").GetProperty("text").GetString());
    }

    [Fact]
    public async Task MATCH3RejectionTheAccountThatDoesNotFitIsNeverListed()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Fair project", "public");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        // Dana offers another device and has ten accepted contributions. The
        // reputation never puts an account in the list.
        await _host.SignInAsync("dana");
        await _host.DeclareCapabilityAsync("device", "ThinkPad", ["lenovo"]);

        await _host.WithDatabaseAsync(async dbContext =>
        {
            var dana = await dbContext.Users.AsNoTracking()
                .FirstAsync(item => item.Username == "dana");

            dbContext.ReputationScores.Add(new ReputationScore
            {
                UserId = dana.Id,
                AcceptedContributions = 10,
                ConfirmedFindings = 10,
                ReversedContributions = 0,
                CurrentScore = 100,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            await dbContext.SaveChangesAsync();
        });

        await _host.SignInAsync("alice");

        var response = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await _host.ReadJsonAsync(response);
        var candidates = document.RootElement.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Empty(candidates);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("dana", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MATCH3BoundaryTheAnswerCarriesNoScoreAndTheOrderIsStable()
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync("Fair project", "public");
        var taskId = await _host.PublishTaskAsync(projectId, "public", ("device", Device));

        await _host.SignInAsync("bob");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        await _host.SignInAsync("erin");
        await _host.DeclareCapabilityAsync("device", Device, ["macbook"]);

        await _host.SignInAsync("alice");

        var first = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");
        var second = await _host.SendAsync(HttpMethod.Get, $"/v1/tasks/{taskId}/candidates");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstBody = await first.Content.ReadAsStringAsync(CancellationToken.None);
        var secondBody = await second.Content.ReadAsStringAsync(CancellationToken.None);

        // The order is stable for the same state.
        Assert.Equal(firstBody, secondBody);

        // The answer carries no score, no weight, no percentage and no rank.
        Assert.DoesNotContain("score", firstBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", firstBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("percentage", firstBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("percent", firstBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rank", firstBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quality", firstBody, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(firstBody);
        var candidates = document.RootElement.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal(2, candidates.Length);

        // The oldest account of the group comes first, so the order is stable
        // for the same state and not for the moment of the read.
        Assert.Equal("bob", candidates[0].GetProperty("username").GetString());
        Assert.Equal("erin", candidates[1].GetProperty("username").GetString());
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
