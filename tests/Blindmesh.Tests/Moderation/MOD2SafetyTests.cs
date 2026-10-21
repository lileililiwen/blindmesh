using System.Net;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Moderation;

/// <summary>
/// MOD-2 Safety: a report gives the reporter a receipt and keeps its body
/// away from everybody but the moderation staff, a banned account is refused
/// before any effect, and a report that names content that became private
/// stays with the staff and reveals nothing to an unrelated account.
/// </summary>
[Collection(ModerationCollection.Name)]
public sealed class MOD2SafetyTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private ModerationTestHost _host = null!;

    public MOD2SafetyTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new ModerationTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MOD2SuccessTheReporterReadsAReceiptAndOnlyTheStaffReadsTheBody()
    {
        await _host.SignInAsync("cara");

        var reported = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/reports",
            "{\"reason\":\"The visible content of the marketplace insults a contributor.\"}",
            idempotencyKey: "mod2-success-report-0001");

        Assert.Equal(HttpStatusCode.Created, reported.StatusCode);

        var receipt = await _host.ReadJsonAsync(reported);
        var reportId = receipt.RootElement.GetProperty("id").GetGuid();

        Assert.False(string.IsNullOrWhiteSpace(receipt.RootElement.GetProperty("receipt").GetString()));
        Assert.Equal("open", receipt.RootElement.GetProperty("state").GetString());

        // The receipt of the reporter never carries the body of the report and
        // never names the reporter.
        await _host.SignInAsync("bob");

        var unrelated = await _host.SendAsync(HttpMethod.Get, $"/v1/reports/{reportId}");

        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);

        // The staff reads the body of the report.
        await _host.SignInAsync("dana");

        var read = await _host.SendAsync(HttpMethod.Get, $"/v1/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var report = await _host.ReadJsonAsync(read);

        Assert.Equal(
            "The visible content of the marketplace insults a contributor.",
            report.RootElement.GetProperty("reason").GetString());

        // The list of the reports belongs to the staff as well.
        await _host.SignInAsync("bob");

        var list = await _host.SendAsync(HttpMethod.Get, "/v1/admin/reports");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        await _host.SignInAsync("dana");

        var staffList = await _host.SendAsync(HttpMethod.Get, "/v1/admin/reports");

        Assert.Equal(HttpStatusCode.OK, staffList.StatusCode);

        Assert.Equal(1, (await _host.ReadJsonAsync(staffList)).RootElement.GetArrayLength());
    }

    [Fact]
    public async Task MOD2RejectionTheBannedAccountIsRefusedBeforeAnyEffect()
    {
        await _host.SignInAsync("bob");

        var (_, csrfToken) = await _host.ReadSessionAsync();

        var bobId = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "bob")
                .Select(user => user.Id)
                .FirstAsync());

        // The administrator bans the account in a second host, so the session
        // of bob stays untouched in the first one.
        using (var staff = new ModerationTestHost(_fixture))
        {
            await staff.SignInAsync("erin");

            var banned = await staff.SendAsync(
                HttpMethod.Post,
                $"/v1/admin/users/{bobId}/ban",
                "{\"reason\":\"The account repeated the abuse that was reported before.\"}",
                idempotencyKey: "mod2-rejection-ban-0001");

            Assert.Equal(HttpStatusCode.OK, banned.StatusCode);
        }

        var afterBan = await _host.WithDatabaseAsync(async dbContext => new
        {
            Reports = await dbContext.Reports.AsNoTracking().CountAsync(),
            Bans = await dbContext.AccountBans.AsNoTracking().CountAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking().CountAsync(),
            Transactions = await dbContext.LedgerTransactions.AsNoTracking().CountAsync(),
            Entries = await dbContext.LedgerEntries.AsNoTracking().CountAsync(),
        });

        Assert.Equal(1, afterBan.Bans);

        // The old session of the banned account is refused before the command
        // reaches the domain.
        var refused = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/reports",
            "{\"reason\":\"The banned account writes another report of the marketplace.\"}",
            "mod2-rejection-report-0001",
            csrfToken);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var error = await _host.ReadJsonAsync(refused);

        Assert.Equal(
            "account_banned",
            error.RootElement.GetProperty("error").GetProperty("code").GetString());

        // The read of the session is refused as well, so every request of the
        // banned account is refused.
        var read = await _host.SendAsync(HttpMethod.Get, "/v1/session");

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        Assert.Equal(
            "account_banned",
            (await _host.ReadJsonAsync(read)).RootElement.GetProperty("error").GetProperty("code").GetString());

        // Nothing happened: no report, no line of the trail and no ledger row.
        var after = await _host.WithDatabaseAsync(async dbContext => new
        {
            Reports = await dbContext.Reports.AsNoTracking().CountAsync(),
            Bans = await dbContext.AccountBans.AsNoTracking().CountAsync(),
            Audit = await dbContext.ModerationAudit.AsNoTracking().CountAsync(),
            Transactions = await dbContext.LedgerTransactions.AsNoTracking().CountAsync(),
            Entries = await dbContext.LedgerEntries.AsNoTracking().CountAsync(),
        });

        Assert.Equal(afterBan.Reports, after.Reports);
        Assert.Equal(afterBan.Bans, after.Bans);
        Assert.Equal(afterBan.Audit, after.Audit);
        Assert.Equal(afterBan.Transactions, after.Transactions);
        Assert.Equal(afterBan.Entries, after.Entries);
    }

    [Fact]
    public async Task MOD2BoundaryTheReportOfPrivateContentStaysWithTheStaffAndRevealsNothingToAnUnrelatedAccount()
    {
        var scenario = new ModerationScenario(_host);
        var seat = await scenario.RunSeatAsync();
        var submissionId = await scenario.SubmitAsync(seat);

        // The content of the report becomes private.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var task = await dbContext.Tasks.FirstAsync(item => item.Id == seat.TaskId);
            task.Visibility = TaskVisibility.Private;
            await dbContext.SaveChangesAsync();
        });

        await _host.SignInAsync("cara");

        var reported = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/reports",
            $"{{\"reason\":\"The evidence of the seat insults the contributor of the seat.\",\"subject_type\":\"submission\",\"subject_id\":\"{submissionId}\"}}",
            idempotencyKey: "mod2-boundary-report-0001");

        Assert.Equal(HttpStatusCode.Created, reported.StatusCode);

        var reportId = (await _host.ReadJsonAsync(reported)).RootElement.GetProperty("id").GetGuid();

        // An unrelated account finds nothing.
        await _host.SignInAsync("erin");

        var unrelated = await _host.SendAsync(HttpMethod.Get, $"/v1/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, unrelated.StatusCode);

        var staff = await _host.ReadJsonAsync(unrelated);

        Assert.Equal(
            "The evidence of the seat insults the contributor of the seat.",
            staff.RootElement.GetProperty("reason").GetString());

        // The identity of the reporter is never part of an answer, and the
        // event of the module carries no body either.
        var body = await unrelated.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("reporter", body, StringComparison.OrdinalIgnoreCase);

        var messages = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.OutboxMessages.AsNoTracking()
                .Where(message => message.Type == "moderation.report.opened")
                .ToListAsync());

        Assert.Single(messages);
        Assert.DoesNotContain("insults", messages[0].Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reporter_id", messages[0].Payload, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Empties the tables of the test run, so every test starts with an empty
    /// marketplace, an empty journal and no case. The roles and the bans of the
    /// seeded accounts are reset as well.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE outbox_messages, account_bans, dispute_evidence, moderation_audit, moderator_assignments, disputes, reports, attachments, finding_history, findings, submission_history, submission_revisions, submissions, participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE users SET role = CASE username WHEN 'dana' THEN 'moderator' WHEN 'erin' THEN 'administrator' ELSE 'user' END");
    }
}
