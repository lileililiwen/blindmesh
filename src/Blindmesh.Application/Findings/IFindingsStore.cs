using Blindmesh.Domain.Findings;

namespace Blindmesh.Application.Findings;

/// <summary>
/// The persistence of the findings module. The store is scoped, so it shares
/// the scoped database context and the transaction of the request with the
/// escrow primitives of the credit module. The lock of a submission comes
/// after the lock of the task and of the seat and before the lock of the
/// accounts, as the shared contract of the lock order demands.
/// </summary>
public interface IFindingsStore
{
    /// <summary>The submission of the given id. Null when there is no such submission.</summary>
    Task<Submission?> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>The submission of a seat. Null when the seat sent no evidence yet.</summary>
    Task<Submission?> FindSubmissionByParticipationAsync(
        Guid participationId,
        CancellationToken cancellationToken);

    /// <summary>Locks one submission for the transaction of the caller.</summary>
    Task<Submission?> LockSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>The revision of the given id. Null when there is no such revision.</summary>
    Task<SubmissionRevision?> FindRevisionAsync(Guid revisionId, CancellationToken cancellationToken);

    /// <summary>The revisions of a submission, in the order of their number.</summary>
    Task<IReadOnlyList<SubmissionRevision>> ListRevisionsAsync(
        Guid submissionId,
        CancellationToken cancellationToken);

    /// <summary>The finding of the given id. Null when there is no such finding.</summary>
    Task<Finding?> FindFindingAsync(Guid findingId, CancellationToken cancellationToken);

    /// <summary>Locks one finding for the transaction of the caller.</summary>
    Task<Finding?> LockFindingAsync(Guid findingId, CancellationToken cancellationToken);

    /// <summary>The findings of a submission, in the order of their creation.</summary>
    Task<IReadOnlyList<Finding>> ListFindingsAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>The history of a finding, in the order of the changes.</summary>
    Task<IReadOnlyList<FindingHistoryEntry>> ListFindingHistoryAsync(
        Guid findingId,
        CancellationToken cancellationToken);

    /// <summary>The attachment of the given id. Null when there is no such attachment.</summary>
    Task<Attachment?> FindAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>Locks one attachment for the transaction of the caller.</summary>
    Task<Attachment?> LockAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>The attachments of a revision, in the order of their creation.</summary>
    Task<IReadOnlyList<Attachment>> ListAttachmentsAsync(
        Guid revisionId,
        CancellationToken cancellationToken);

    /// <summary>The number of the attachments of a revision.</summary>
    Task<int> CountAttachmentsAsync(Guid revisionId, CancellationToken cancellationToken);

    /// <summary>The history of a submission, in the order of the changes.</summary>
    Task<IReadOnlyList<SubmissionStatusChange>> ListSubmissionHistoryAsync(
        Guid submissionId,
        CancellationToken cancellationToken);

    /// <summary>The submissions whose review is overdue, oldest first.</summary>
    Task<IReadOnlyList<Submission>> ListOverdueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// The submissions that wait for a correction that was not sent in time,
    /// oldest first.
    /// </summary>
    Task<IReadOnlyList<Submission>> ListOverdueCorrectionsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// The evidence of the tasks of a project, with the task they belong to.
    /// </summary>
    Task<IReadOnlyList<SubmissionReviewRow>> ListReviewsAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task AddSubmissionAsync(Submission submission, CancellationToken cancellationToken);

    Task AddRevisionAsync(SubmissionRevision revision, CancellationToken cancellationToken);

    Task AddFindingAsync(Finding finding, CancellationToken cancellationToken);

    Task AddFindingHistoryAsync(FindingHistoryEntry entry, CancellationToken cancellationToken);

    Task AddAttachmentAsync(Attachment attachment, CancellationToken cancellationToken);

    Task AddStatusChangeAsync(SubmissionStatusChange change, CancellationToken cancellationToken);

    /// <summary>Writes the changes of the caller. False when another request changed the row first.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
