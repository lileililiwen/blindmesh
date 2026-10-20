using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Findings;

/// <summary>
/// EVID-1 Evidence: a contributor submits the protected evidence of a seat they
/// are running, the base of the seat is pending and the reviewers read only the
/// evidence they may read. A file that is oversized, mismatched or unsafe never
/// becomes readable and it never leaks a public address or a detail of the
/// project, of the owner or of the contributor. Every address of a file is
/// signed for a short time and the authorization is checked again for it.
/// </summary>
[Collection(FindingsCollection.Name)]
public sealed class EVID1EvidenceTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    private FindingsTestHost _host = null!;

    public EVID1EvidenceTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _host = new FindingsTestHost(_fixture);
        await ResetAsync(_fixture);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EVID1SuccessTheContributorSubmitsTheEvidenceAndTheOwnerReadsTheScannedFile()
    {
        var seat = await RunSeatAsync(modes: ["verify"], acceptsCleanFindings: false);

        // The contributor sends the evidence of the seat.
        await _host.SignInAsync("bob");

        var submitted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{seat.ParticipationId}/submissions",
            """
            {
              "summary":"I walked through the seat and found one broken flow.",
              "scope_completed":true,
              "steps":[{"position":1,"title":"Open the task page","result":"The page shows the seat."}],
              "findings":[
                {"category":"bug","severity":"medium","title":"The seat is empty","reproduction":"Open the seat and read the list.","expected_result":"The seat shows the task.","actual_result":"The seat is empty."}
              ]
            }
            """,
            idempotencyKey: "evid1-success-submit-0001");

        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        var submission = await ReadJsonAsync(submitted);

        Assert.Equal("submitted", submission.RootElement.GetProperty("state").GetString());
        Assert.Equal("bob", submission.RootElement.GetProperty("contributor_username").GetString());
        Assert.True(submission.RootElement.GetProperty("scope_completed").GetBoolean());
        Assert.Equal(1, submission.RootElement.GetProperty("revisions").GetArrayLength());
        Assert.Equal(1, submission.RootElement.GetProperty("findings").GetArrayLength());

        var submissionId = submission.RootElement.GetProperty("id").GetGuid();

        // The base of the seat is pending and the seat and the slot carry the
        // state of the evidence.
        var committed = await _host.WithDatabaseAsync(async dbContext => new
        {
            Participation = await dbContext.Participations.AsNoTracking()
                .FirstAsync(item => item.Id == seat.ParticipationId),
            Slot = await dbContext.TaskSlots.AsNoTracking()
                .FirstAsync(slot => slot.TaskId == seat.TaskId && slot.ContributorId != null),
            Submission = await dbContext.Submissions.AsNoTracking()
                .FirstAsync(item => item.Id == submissionId),
            Pending = await dbContext.LedgerEntries.AsNoTracking()
                .Where(entry => dbContext.LedgerAccounts
                    .Any(account => account.Id == entry.AccountId && account.Bucket == LedgerBucket.Pending))
                .SumAsync(entry => (long?)entry.Amount) ?? 0,
            Entries = await dbContext.LedgerEntries.AsNoTracking().ToListAsync(),
        });

        Assert.Equal(ParticipationState.Submitted, committed.Participation.State);
        Assert.Equal(TaskSlotState.Submitted, committed.Slot.State);
        Assert.Equal(SubmissionState.Submitted, committed.Submission.State);
        Assert.Equal(10, committed.Pending);
        Assert.All(
            committed.Entries.GroupBy(entry => entry.TransactionId),
            transaction => Assert.Equal(0, transaction.Sum(entry => entry.Amount)));

        // The owner reads the evidence.
        await _host.SignInAsync("alice");

        var read = await _host.SendAsync(HttpMethod.Get, $"/v1/submissions/{submissionId}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var detail = await ReadJsonAsync(read);

        Assert.Equal("submitted", detail.RootElement.GetProperty("state").GetString());
        Assert.Equal("alice", detail.RootElement.GetProperty("owner_username").GetString());
        Assert.Equal(1, detail.RootElement.GetProperty("revisions").GetArrayLength());
        Assert.Equal(
            "The seat is empty",
            detail.RootElement.GetProperty("findings")[0].GetProperty("title").GetString());

        // The contributor declares a private file. It is quarantined from this
        // moment and it is never readable while it waits.
        await _host.SignInAsync("bob");

        var intent = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/attachments/intents",
            $$"""
              {"submission_id":"{{submissionId:D}}","file_name":"evidence.log","content_type":"text/plain","size_bytes":11}
              """,
            idempotencyKey: "evid1-success-intent-0001");

        Assert.Equal(HttpStatusCode.Created, intent.StatusCode);

        var declared = await ReadJsonAsync(intent);

        Assert.Equal("quarantined", declared.RootElement.GetProperty("state").GetString());

        var attachmentId = declared.RootElement.GetProperty("id").GetGuid();

        var quarantined = await _host.SendAsync(HttpMethod.Get, $"/v1/attachments/{attachmentId}/download");

        Assert.Equal(HttpStatusCode.Conflict, quarantined.StatusCode);

        // The contributor uploads the bytes and the file leaves the quarantine.
        var uploaded = await _host.UploadAttachmentAsync(
            attachmentId,
            "evidence.log",
            "text/plain",
            "hello world"u8.ToArray(),
            "evid1-success-upload-0001");

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);

        var released = await ReadJsonAsync(uploaded);

        Assert.Equal("scanned", released.RootElement.GetProperty("state").GetString());

        // The owner signs the address of the file. It lives for a short time.
        await _host.SignInAsync("alice");

        var download = await _host.SendAsync(HttpMethod.Get, $"/v1/attachments/{attachmentId}/download");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        var address = await ReadJsonAsync(download);
        var expiresAt = address.RootElement.GetProperty("expires_at").GetDateTimeOffset();
        var lifetime = expiresAt - DateTimeOffset.UtcNow;

        Assert.False(string.IsNullOrWhiteSpace(address.RootElement.GetProperty("url").GetString()));
        Assert.InRange(lifetime.TotalSeconds, 1, SubmissionRules.DownloadUrlSeconds);
    }

    [Fact]
    public async Task EVID1SuccessACleanExplorationIsAcceptedWithItsExplanation()
    {
        var seat = await RunSeatAsync(modes: ["explore"], acceptsCleanFindings: true);

        await _host.SignInAsync("bob");

        var submitted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{seat.ParticipationId}/submissions",
            """
            {
              "summary":"I explored the whole area and found nothing.",
              "scope_completed":true,
              "tested_areas":["the sign in page","the seat list"],
              "clean_result_explanation":"Every flow behaved as the documentation says.",
              "findings":[]
            }
            """,
            idempotencyKey: "evid1-success-clean-0001");

        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        var submission = await ReadJsonAsync(submitted);

        Assert.Equal("submitted", submission.RootElement.GetProperty("state").GetString());
        Assert.Equal(0, submission.RootElement.GetProperty("findings").GetArrayLength());

        var revision = submission.RootElement.GetProperty("revisions")[0];

        Assert.Equal(2, revision.GetProperty("tested_areas").GetArrayLength());
        Assert.Equal(
            "Every flow behaved as the documentation says.",
            revision.GetProperty("clean_result_explanation").GetString());
    }

    [Fact]
    public async Task EVID1RejectionAnUnsafeOrOversizedFileIsRefusedAndAnotherParticipantReadsNothing()
    {
        var seat = await RunSeatAsync(modes: ["verify"], acceptsCleanFindings: false);

        await _host.SignInAsync("bob");

        var submitted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{seat.ParticipationId}/submissions",
            """
            {
              "summary":"I walked through the seat.",
              "scope_completed":true,
              "steps":[{"position":1,"title":"Open the task page","result":"The page shows the seat."}],
              "findings":[]
            }
            """,
            idempotencyKey: "evid1-rejection-submit-0001");

        var submissionId = (await ReadJsonAsync(submitted)).RootElement.GetProperty("id").GetGuid();

        // An executable is refused.
        var executable = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/attachments/intents",
            $$"""
              {"submission_id":"{{submissionId:D}}","file_name":"payload.exe","content_type":"application/octet-stream","size_bytes":11}
              """,
            idempotencyKey: "evid1-rejection-exe-0001");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, executable.StatusCode);

        // An archive is refused.
        var archive = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/attachments/intents",
            $$"""
              {"submission_id":"{{submissionId:D}}","file_name":"evidence.zip","content_type":"application/zip","size_bytes":11}
              """,
            idempotencyKey: "evid1-rejection-zip-0001");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, archive.StatusCode);

        // A file that is larger than the allowed size is refused.
        var oversized = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/attachments/intents",
            $$"""
              {"submission_id":"{{submissionId:D}}","file_name":"evidence.log","content_type":"text/plain","size_bytes":3145728}
              """,
            idempotencyKey: "evid1-rejection-large-0001");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, oversized.StatusCode);

        // No file of the refused declarations exists.
        var stored = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Attachments.AsNoTracking().CountAsync());

        Assert.Equal(0, stored);

        // Another participant never reads the evidence of the seat and never
        // learns the project, the owner or the contributor.
        await _host.SignInAsync("dana");

        var other = await _host.SendAsync(HttpMethod.Get, $"/v1/submissions/{submissionId}");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        var body = await other.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("bob", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seat.ProjectId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EVID1BoundaryTheMembershipChangeRefusesTheAddressOfTheFile()
    {
        var seat = await RunSeatAsync(modes: ["verify"], acceptsCleanFindings: false);

        await _host.SignInAsync("bob");

        var submitted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{seat.ParticipationId}/submissions",
            """
            {
              "summary":"I walked through the seat.",
              "scope_completed":true,
              "steps":[{"position":1,"title":"Open the task page","result":"The page shows the seat."}],
              "findings":[]
            }
            """,
            idempotencyKey: "evid1-boundary-submit-0001");

        var submissionId = (await ReadJsonAsync(submitted)).RootElement.GetProperty("id").GetGuid();

        var intent = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/attachments/intents",
            $$"""
              {"submission_id":"{{submissionId:D}}","file_name":"evidence.log","content_type":"text/plain","size_bytes":11}
              """,
            idempotencyKey: "evid1-boundary-intent-0001");

        var attachmentId = (await ReadJsonAsync(intent)).RootElement.GetProperty("id").GetGuid();

        var uploaded = await _host.UploadAttachmentAsync(
            attachmentId,
            "evidence.log",
            "text/plain",
            "hello world"u8.ToArray(),
            "evid1-boundary-upload-0001");

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);

        // The contributor holds an address of the file while the project is
        // visible for him.
        var first = await _host.SendAsync(HttpMethod.Get, $"/v1/attachments/{attachmentId}/download");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The owner closes the project and the contributor is not a member of it.
        await _host.SignInAsync("alice");

        var projectVersion = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Projects.AsNoTracking()
                .Where(project => project.Id == seat.ProjectId)
                .Select(project => project.Version)
                .FirstAsync());

        var closed = await _host.SendAsync(
            HttpMethod.Patch,
            $"/v1/projects/{seat.ProjectId}",
            $"{{\"expected_version\":{projectVersion},\"visibility\":\"private\"}}");

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        // The authorization is checked again, so the former reader never gets a
        // new address.
        await _host.SignInAsync("bob");

        var second = await _host.SendAsync(HttpMethod.Get, $"/v1/attachments/{attachmentId}/download");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("evidence.log", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Publishes a task, gives a seat to bob and lets him start the work.
    /// </summary>
    private async Task<Seat> RunSeatAsync(string[] modes, bool acceptsCleanFindings)
    {
        await _host.SignInAsync("alice");
        await _host.ClaimGrantAsync();

        var projectId = await _host.CreateActiveProjectAsync();

        var created = await _host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            FindingsTestHost.TaskBody(
                projectId,
                modes: modes,
                acceptsCleanFindings: acceptsCleanFindings,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: modes.Contains("verify")
                    ? ["{\"title\":\"Open the task page\",\"expected_result\":\"The page shows the seat.\"}"]
                    : null,
                deadline: FindingsTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = (await ReadJsonAsync(created)).RootElement.GetProperty("id").GetGuid();

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"evid1-publish-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var taskVersion = await _host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.Version)
                .FirstAsync());

        await _host.SignInAsync("bob");
        await _host.OfferCapabilityAsync();

        var applied = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{taskVersion}}}",
            idempotencyKey: $"evid1-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        var participationId = (await ReadJsonAsync(applied)).RootElement.GetProperty("id").GetGuid();

        await _host.SignInAsync("alice");

        var accepted = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"evid1-accept-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await _host.SignInAsync("bob");

        var started = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: $"evid1-start-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        return new Seat(taskId, Guid.Parse(projectId), participationId);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonDocument.Parse(body);
    }

    /// <summary>A running seat of a published task.</summary>
    private sealed record Seat(Guid TaskId, Guid ProjectId, Guid ParticipationId);

    /// <summary>
    /// Empties the tables of the test run, so every test starts with an empty
    /// marketplace, an empty journal and no evidence.
    /// </summary>
    private static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE attachments, finding_history, findings, submission_history, submission_revisions, submissions, participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");
    }
}
