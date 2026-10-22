using System.Net;
using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Operations;

/// <summary>
/// OPS-2 PilotGate: the gate of the pilot reports the measured reality and
/// never the target as a measurement. A target that has no observation window
/// yet stays a target, the release stays blocked with the exact failed command
/// and the next action, and the authorization of the area is checked on every
/// request.
/// </summary>
[Collection(OperationsCollection.Name)]
public sealed class OPS2PilotGateTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private OperationsTestHost _host = null!;

    public OPS2PilotGateTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new OperationsTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OPS2SuccessTheGateReportsTheMeasuredCountsAndTheRecordedEvidenceOfThePilot()
    {
        await _host.SignInAsync("erin");

        var rehearsal = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/admin/operations/restores",
            """{"browser_journeys":7,"axe_runs":5}""");

        Assert.Equal(HttpStatusCode.OK, rehearsal.StatusCode);

        var gate = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.OK, gate.StatusCode);

        var measured = await _host.ReadJsonAsync(gate);

        // The counts of the gate are the counts of the database and not a
        // target of the roadmap.
        var accepted = await CountAsync("SELECT count(*) FROM submissions WHERE state = 'Accepted'");
        var openCases = await CountAsync("SELECT count(*) FROM disputes WHERE state IN ('Open', 'EvidenceRequested')");
        var deadLettered = await CountAsync("SELECT count(*) FROM notification_deliveries WHERE status = 'DeadLettered'");

        Assert.Equal(
            accepted.ToString(),
            FindItem(measured.RootElement, "accepted_contributions").GetProperty("measured").GetString());
        Assert.Equal(
            openCases.ToString(),
            FindItem(measured.RootElement, "open_cases").GetProperty("measured").GetString());
        Assert.Equal(
            deadLettered.ToString(),
            FindItem(measured.RootElement, "dead_lettered_deliveries").GetProperty("measured").GetString());

        // The command of every item is the command that produced the value.
        Assert.Contains(
            "FROM submissions",
            FindItem(measured.RootElement, "accepted_contributions").GetProperty("command").GetString(),
            StringComparison.Ordinal);

        // The numbers the operator recorded are reported as they are.
        Assert.Equal(
            "7",
            FindItem(measured.RootElement, "browser_journeys").GetProperty("measured").GetString());
        Assert.True(FindItem(measured.RootElement, "browser_journeys").GetProperty("passed").GetBoolean());
        Assert.Equal(
            "5",
            FindItem(measured.RootElement, "axe_runs").GetProperty("measured").GetString());
        Assert.True(FindItem(measured.RootElement, "axe_runs").GetProperty("passed").GetBoolean());
        Assert.Equal(
            "passed",
            FindItem(measured.RootElement, "restore_rehearsal").GetProperty("measured").GetString());
        Assert.True(FindItem(measured.RootElement, "restore_rehearsal").GetProperty("passed").GetBoolean());

        // Every item names the command that produced its value.
        foreach (var item in measured.RootElement.GetProperty("items").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("command").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("expected").GetString()));
        }
    }

    [Fact]
    public async Task OPS2RejectionTheGateIsRefusedAndTheReleaseStaysBlockedWithTheFailedCommand()
    {
        // A visitor without a session is refused.
        var visitor = await _host.CreateClientWithoutCookies().GetAsync("/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.Unauthorized, visitor.StatusCode);

        // An ordinary account is refused.
        await _host.SignInAsync("alice");

        var account = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.Forbidden, account.StatusCode);

        // A moderator is refused as well.
        await _host.SignInAsync("dana");

        var moderator = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.Forbidden, moderator.StatusCode);

        // The administrator reads a gate that is blocked, because the restore
        // evidence is missing.
        await _host.SignInAsync("erin");

        var gate = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.OK, gate.StatusCode);

        var measured = await _host.ReadJsonAsync(gate);

        Assert.False(measured.RootElement.GetProperty("ready").GetBoolean());

        var rehearsal = FindItem(measured.RootElement, "restore_rehearsal");

        Assert.Equal("not yet run", rehearsal.GetProperty("measured").GetString());
        Assert.False(rehearsal.GetProperty("passed").GetBoolean());
        Assert.Equal(
            "POST /v1/admin/operations/restores",
            rehearsal.GetProperty("command").GetString());

        // The blocked items name the command that has to run next.
        foreach (var item in measured.RootElement.GetProperty("items").EnumerateArray())
        {
            if (!item.GetProperty("passed").GetBoolean())
            {
                Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("command").GetString()));
            }
        }
    }

    [Fact]
    public async Task OPS2BoundaryTheLatencyAndTheAvailabilityStayTargetsUntilTheirWindowIsDocumented()
    {
        await _host.SignInAsync("erin");

        var gate = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.OK, gate.StatusCode);

        var measured = await _host.ReadJsonAsync(gate);

        var latency = FindItem(measured.RootElement, "api_latency_p95");

        Assert.Equal("target, not yet measured", latency.GetProperty("measured").GetString());
        Assert.Equal("below 500 ms", latency.GetProperty("expected").GetString());
        Assert.False(latency.GetProperty("passed").GetBoolean());

        var availability = FindItem(measured.RootElement, "availability");

        Assert.Equal("target, not yet measured", availability.GetProperty("measured").GetString());
        Assert.Equal(
            "99.5 percent over a documented observation window",
            availability.GetProperty("expected").GetString());
        Assert.False(availability.GetProperty("passed").GetBoolean());

        // The target is never reported as a measurement.
        Assert.DoesNotContain("500", latency.GetProperty("measured").GetString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("99.5", availability.GetProperty("measured").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OPS2ThePublicStatusHoldsTheStateOfTheRuntimeAndNoPrivateData()
    {
        var response = await _host.CreateClientWithoutCookies().GetAsync("/v1/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("@blindmesh.test", body, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", body, StringComparison.Ordinal);

        var status = JsonDocument.Parse(body).RootElement;

        Assert.False(string.IsNullOrWhiteSpace(status.GetProperty("status").GetString()));
        Assert.True(status.GetProperty("dependencies").GetArrayLength() >= 3);
        Assert.True(status.GetProperty("not_implemented").GetArrayLength() > 0);

        foreach (var dependency in status.GetProperty("dependencies").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(dependency.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(dependency.GetProperty("status").GetString()));
        }

        var marketplace = status.GetProperty("marketplace");

        Assert.True(marketplace.GetProperty("published_tasks").GetInt64() >= 0);
        Assert.True(marketplace.GetProperty("accepted_contributions").GetInt64() >= 0);
        Assert.True(marketplace.GetProperty("open_cases").GetInt64() >= 0);
        Assert.True(marketplace.GetProperty("contributors").GetInt64() >= 0);
    }

    private Task<long> CountAsync(string sql) =>
        _host.WithDatabaseAsync(async dbContext =>
        {
            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var value = await command.ExecuteScalarAsync();

            return value is null or DBNull ? 0 : Convert.ToInt64(value);
        });

    private static JsonElement FindItem(JsonElement gate, string name)
    {
        foreach (var item in gate.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("name").GetString() == name)
            {
                return item;
            }
        }

        throw new InvalidOperationException($"The gate does not hold the item {name}.");
    }

    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE operations_deletion_requests, operations_restore_rehearsals, operations_backups, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims CASCADE");
    }
}
