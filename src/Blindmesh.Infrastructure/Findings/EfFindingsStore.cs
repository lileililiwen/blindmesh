using Blindmesh.Application.Findings;
using Blindmesh.Domain.Findings;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Findings;

/// <summary>
/// The persistence of the findings. The store is scoped, so it shares the
/// scoped database context and the transaction of the request with the escrow
/// primitives of the credit module. The lock of a submission comes after the
/// lock of the task and of the seat and before the lock of the accounts, as the
/// shared contract of the lock order demands.
/// </summary>
public sealed class EfFindingsStore(BlindmeshDbContext dbContext) : IFindingsStore
{
    public Task<Submission?> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        dbContext.Submissions.FirstOrDefaultAsync(
            submission => submission.Id == submissionId,
            cancellationToken);

    public Task<Submission?> FindSubmissionByParticipationAsync(
        Guid participationId,
        CancellationToken cancellationToken) =>
        dbContext.Submissions.FirstOrDefaultAsync(
            submission => submission.ParticipationId == participationId,
            cancellationToken);

    public async Task<Submission?> LockSubmissionAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var locked = await LockRowAsync("submissions", submissionId, cancellationToken);

        if (locked is null)
        {
            return null;
        }

        return await dbContext.Submissions.FirstOrDefaultAsync(
            submission => submission.Id == submissionId,
            cancellationToken);
    }

    public Task<SubmissionRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken) =>
        dbContext.SubmissionRevisions.FirstOrDefaultAsync(
            revision => revision.Id == revisionId,
            cancellationToken);

    public async Task<IReadOnlyList<SubmissionRevision>> ListRevisionsAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.SubmissionRevisions
            .Where(revision => revision.SubmissionId == submissionId)
            .OrderBy(revision => revision.Number)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task<Finding?> FindFindingAsync(Guid findingId, CancellationToken cancellationToken) =>
        dbContext.Findings.FirstOrDefaultAsync(finding => finding.Id == findingId, cancellationToken);

    public async Task<Finding?> LockFindingAsync(Guid findingId, CancellationToken cancellationToken)
    {
        var locked = await LockRowAsync("findings", findingId, cancellationToken);

        if (locked is null)
        {
            return null;
        }

        return await dbContext.Findings.FirstOrDefaultAsync(
            finding => finding.Id == findingId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Finding>> ListFindingsAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Findings
            .Where(finding => finding.SubmissionId == submissionId)
            .OrderBy(finding => finding.CreatedAt)
            .ThenBy(finding => finding.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<FindingHistoryEntry>> ListFindingHistoryAsync(
        Guid findingId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.FindingHistory
            .Where(entry => entry.FindingId == findingId)
            .OrderBy(entry => entry.ChangedAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task<Attachment?> FindAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        dbContext.Attachments.FirstOrDefaultAsync(
            attachment => attachment.Id == attachmentId,
            cancellationToken);

    public async Task<Attachment?> LockAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        var locked = await LockRowAsync("attachments", attachmentId, cancellationToken);

        if (locked is null)
        {
            return null;
        }

        return await dbContext.Attachments.FirstOrDefaultAsync(
            attachment => attachment.Id == attachmentId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Attachment>> ListAttachmentsAsync(
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Attachments
            .Where(attachment => attachment.RevisionId == revisionId)
            .OrderBy(attachment => attachment.CreatedAt)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task<int> CountAttachmentsAsync(Guid revisionId, CancellationToken cancellationToken) =>
        dbContext.Attachments.CountAsync(
            attachment => attachment.RevisionId == revisionId,
            cancellationToken);

    public async Task<IReadOnlyList<SubmissionStatusChange>> ListSubmissionHistoryAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.SubmissionHistory
            .Where(change => change.SubmissionId == submissionId)
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<Submission>> ListOverdueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Submissions
            .Where(submission => submission.State == SubmissionState.Submitted &&
                submission.CorrectionRequestedAt == null &&
                submission.SubmittedAt != null &&
                submission.SubmittedAt <= now.AddHours(-SubmissionRules.ReviewTimeoutHours))
            .OrderBy(submission => submission.SubmittedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<Submission>> ListOverdueCorrectionsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Submissions
            .Where(submission => submission.State == SubmissionState.ChangesRequested &&
                submission.CorrectionDeadline != null &&
                submission.CorrectionDeadline <= now)
            .OrderBy(submission => submission.CorrectionDeadline)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<SubmissionReviewRow>> ListReviewsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var rows = await (from submission in dbContext.Submissions.AsNoTracking()
                          join task in dbContext.Tasks.AsNoTracking()
                              on submission.TaskId equals task.Id
                          where submission.ProjectId == projectId
                          orderby submission.SubmittedAt, submission.Id
                          select new SubmissionReviewRow(
                              submission.Id,
                              task.Id,
                              task.Title,
                              task.OwnerId,
                              submission.ContributorId,
                              submission.State,
                              submission.Summary,
                              submission.ScopeCompleted,
                              submission.SubmittedAt,
                              submission.CorrectionDeadline,
                              task.BaseReward,
                              task.SlotBonus,
                              submission.BonusGranted,
                              submission.Version))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task AddSubmissionAsync(Submission submission, CancellationToken cancellationToken) =>
        dbContext.Submissions.AddAsync(submission, cancellationToken).AsTask();

    public Task AddRevisionAsync(SubmissionRevision revision, CancellationToken cancellationToken) =>
        dbContext.SubmissionRevisions.AddAsync(revision, cancellationToken).AsTask();

    public Task AddFindingAsync(Finding finding, CancellationToken cancellationToken) =>
        dbContext.Findings.AddAsync(finding, cancellationToken).AsTask();

    public Task AddFindingHistoryAsync(FindingHistoryEntry entry, CancellationToken cancellationToken) =>
        dbContext.FindingHistory.AddAsync(entry, cancellationToken).AsTask();

    public Task AddAttachmentAsync(Attachment attachment, CancellationToken cancellationToken) =>
        dbContext.Attachments.AddAsync(attachment, cancellationToken).AsTask();

    public Task AddStatusChangeAsync(SubmissionStatusChange change, CancellationToken cancellationToken) =>
        dbContext.SubmissionHistory.AddAsync(change, cancellationToken).AsTask();

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken) > 0;

    /// <summary>
    /// Locks one row of the findings for the transaction of the caller. The lock
    /// is taken with a plain statement, so it waits for the writer that holds
    /// the same row and it never breaks the order of the locks of the package.
    /// </summary>
    private async Task<Guid?> LockRowAsync(string table, Guid id, CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A findings command must run inside the transaction of the caller.");

        var connection = dbContext.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = $"SELECT id FROM {table} WHERE id = @id FOR UPDATE";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        var locked = await command.ExecuteScalarAsync(cancellationToken);

        return locked is null ? null : id;
    }
}
