using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Operations;

/// <summary>
/// OPS-1 Recovery: the operator takes a backup, restores it into an isolated
/// database of its own, runs the four checks against that database and drops
/// the isolated database again. A failed check blocks the promotion, and the
/// deletion request of an operator removes the identity of a subject while the
/// settled financial records keep their provenance.
/// </summary>
[Collection(OperationsCollection.Name)]
public sealed class OPS1RecoveryTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private OperationsTestHost _host = null!;

    public OPS1RecoveryTests(RuntimeFixture fixture)
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
    public async Task OPS1SuccessTheOperatorRestoresTheBackupIntoAnIsolatedDatabaseAndEveryCheckPasses()
    {
        await _host.SignInAsync("erin");

        var checksum = new string('a', 64);

        var recorded = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/admin/operations/backups",
            $$"""
              {"kind":"database","location":"nightly-dump-2026-10-22","size_bytes":4096,"checksum_sha256":"{{checksum}}","retention_until":"2026-11-21T00:00:00Z","detail":"The dump of the night before the pilot."}
              """);

        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);

        var backup = await _host.ReadJsonAsync(recorded);

        Assert.Equal("database", backup.RootElement.GetProperty("kind").GetString());
        Assert.Equal(checksum, backup.RootElement.GetProperty("checksum_sha256").GetString());

        var rehearsal = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/admin/operations/restores",
            """{"backup_id":"00000000-0000-0000-0000-000000000000","browser_journeys":7,"axe_runs":5}""");

        // The backup of the test does not exist, so the rehearsal is refused
        // with the record that is not visible.
        Assert.Equal(HttpStatusCode.NotFound, rehearsal.StatusCode);

        var withoutBackup = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/admin/operations/restores",
            """{"browser_journeys":7,"axe_runs":5}""");

        Assert.Equal(HttpStatusCode.OK, withoutBackup.StatusCode);

        var result = await _host.ReadJsonAsync(withoutBackup);

        Assert.Equal("passed", result.RootElement.GetProperty("status").GetString());
        Assert.StartsWith("bm_rehearsal_", result.RootElement.GetProperty("namespace").GetString());
        Assert.Contains(
            "expected schema",
            result.RootElement.GetProperty("schema_check").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "match the live database",
            result.RootElement.GetProperty("data_check").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "matches the live database",
            result.RootElement.GetProperty("object_check").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "sum to zero",
            result.RootElement.GetProperty("ledger_check").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(7, result.RootElement.GetProperty("browser_journeys").GetInt32());
        Assert.Equal(5, result.RootElement.GetProperty("axe_runs").GetInt32());

        // The isolated database is dropped again, so a rehearsal never leaves a
        // database behind.
        var leftovers = await _host.WithDatabaseAsync(async dbContext =>
        {
            var names = new List<string>();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "SELECT datname FROM pg_database WHERE datname LIKE 'bm\\_rehearsal\\_%'";

            var connection = dbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        });

        Assert.Empty(leftovers);

        // The rehearsal is listed for the operator.
        var list = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/restores");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var listed = await _host.ReadJsonAsync(list);

        Assert.Equal(1, listed.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task OPS1RejectionAFailedLedgerCheckBlocksThePromotionAndTheRecoveryPathStaysAvailable()
    {
        await _host.SignInAsync("erin");

        // A transaction of the journal that does not sum to zero. The credits
        // module never writes such a transaction, so the rehearsal of the
        // operator finds it and refuses the promotion.
        await _host.WithDatabaseAsync(async dbContext =>
        {
            var transactionId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            dbContext.LedgerAccounts.Add(new LedgerAccount
            {
                Id = accountId,
                Bucket = LedgerBucket.UserAvailable,
                Reference = "ops-rejection",
                CreatedAt = now,
            });

            dbContext.LedgerTransactions.Add(new LedgerTransaction
            {
                Id = transactionId,
                Kind = "ops_rejection",
                Reference = "ops-rejection",
                CreatedAt = now,
            });

            dbContext.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = transactionId,
                AccountId = accountId,
                Amount = 25,
                CreatedAt = now,
            });

            await dbContext.SaveChangesAsync();
        });

        var rehearsal = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/admin/operations/restores",
            """{"browser_journeys":1,"axe_runs":1}""");

        Assert.Equal(HttpStatusCode.OK, rehearsal.StatusCode);

        var result = await _host.ReadJsonAsync(rehearsal);

        Assert.Equal("failed", result.RootElement.GetProperty("status").GetString());
        Assert.Contains(
            "do not add up to zero",
            result.RootElement.GetProperty("ledger_check").GetString(),
            StringComparison.Ordinal);

        // The promotion is blocked: the gate names the rehearsal that failed.
        var gate = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/gate");

        Assert.Equal(HttpStatusCode.OK, gate.StatusCode);

        var measured = await _host.ReadJsonAsync(gate);

        Assert.False(measured.RootElement.GetProperty("ready").GetBoolean());

        var rehearsalItem = FindItem(measured.RootElement, "restore_rehearsal");

        Assert.Equal("failed", rehearsalItem.GetProperty("measured").GetString());
        Assert.False(rehearsalItem.GetProperty("passed").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(rehearsalItem.GetProperty("command").GetString()));

        // The documented recovery path stays available: the backup of the
        // operator is still listed and the runbook names the restore command.
        var backups = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/backups");

        Assert.Equal(HttpStatusCode.OK, backups.StatusCode);

        var recorded = await _host.ReadJsonAsync(backups);

        Assert.Equal(0, recorded.RootElement.GetProperty("items").GetArrayLength());

        var runbook = Path.Combine(
            RepositoryRoot.Find().FullName,
            "infra",
            "runbooks",
            "backup-restore.md");

        Assert.True(File.Exists(runbook), "The runbook of the backup and the restore is missing.");

        var text = await File.ReadAllTextAsync(runbook);

        Assert.Contains("pg_restore", text, StringComparison.Ordinal);
        Assert.Contains("/v1/admin/operations/restores", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OPS1BoundaryTheDeletionRequestRemovesTheIdentityAndKeepsTheProvenanceOfTheSettledRecords()
    {
        // The contributor holds a settled financial record.
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var subjectId = await _host.FindUserIdAsync("alice");

        Assert.NotNull(subjectId);

        await _host.SignInAsync("erin");

        var request = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/admin/operations/deletions",
            $$"""{"subject_user_id":"{{subjectId}}","scope":"profile and sessions"}""");

        Assert.Equal(HttpStatusCode.Created, request.StatusCode);

        var body = await request.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("alice@blindmesh.test", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"alice\"", body, StringComparison.Ordinal);

        var deletion = await _host.ReadJsonAsync(request);

        Assert.Equal("completed", deletion.RootElement.GetProperty("status").GetString());
        Assert.Equal("profile and sessions", deletion.RootElement.GetProperty("scope").GetString());

        var pseudonym = deletion.RootElement.GetProperty("pseudonymized_user_id").GetString()!;

        Assert.StartsWith("deleted-", pseudonym, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(
            deletion.RootElement.GetProperty("audit_reference").GetString()));

        // The identity of the subject is gone, the account keeps the pseudonym
        // and the account is closed.
        var subject = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == subjectId!.Value));

        Assert.NotNull(subject);
        Assert.Equal(pseudonym, subject!.Username);
        Assert.Equal(string.Empty, subject.Email);
        Assert.Equal(string.Empty, subject.DisplayName);
        Assert.Equal(string.Empty, subject.Bio);
        Assert.Empty(subject.Skills);
        Assert.Empty(subject.Languages);
        Assert.Equal(string.Empty, subject.GithubLogin);
        Assert.False(subject.IsPublic);

        // The settled financial records keep the pseudonym of the account and
        // they never keep the identity of the subject.
        var accounts = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.LedgerAccounts
                .AsNoTracking()
                .Where(account => account.OwnerId == subjectId)
                .ToListAsync());

        Assert.NotEmpty(accounts);

        var owner = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == subjectId!.Value));

        Assert.NotNull(owner);
        Assert.Equal(pseudonym, owner!.Username);

        // The provider identity is gone and the sessions are revoked, so the
        // account can never sign in again.
        var identities = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.ExternalIdentities
                .AsNoTracking()
                .CountAsync(identity => identity.UserId == subjectId!.Value));

        Assert.Equal(0, identities);

        var openSessions = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Sessions
                .AsNoTracking()
                .CountAsync(session => session.UserId == subjectId!.Value && session.RevokedAt == null));

        Assert.Equal(0, openSessions);

        // The request is listed for the operator.
        var list = await _host.SendAsync(HttpMethod.Get, "/v1/admin/operations/deletions");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var listed = await _host.ReadJsonAsync(list);

        Assert.Equal(1, listed.RootElement.GetProperty("items").GetArrayLength());
    }

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
