using Blindmesh.Domain.Moderation;

namespace Blindmesh.Application.Moderation;

/// <summary>
/// The persistence of the moderation module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly. The store is the only place that writes a moderation row.
/// </summary>
public interface IModerationStore
{
    /// <summary>The report with its identifier, when it exists.</summary>
    Task<Report?> FindReportAsync(Guid reportId, CancellationToken cancellationToken);

    /// <summary>The report, locked for the rest of the transaction.</summary>
    Task<Report?> LockReportAsync(Guid reportId, CancellationToken cancellationToken);

    /// <summary>Every report, the newest first.</summary>
    Task<IReadOnlyList<Report>> ListReportsAsync(CancellationToken cancellationToken);

    /// <summary>Writes a new report.</summary>
    Task AddReportAsync(Report report, CancellationToken cancellationToken);

    /// <summary>
    /// The ban of an account. Null means that the account was never banned.
    /// There is one row per account.
    /// </summary>
    Task<AccountBan?> FindBanAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes a new ban of an account.</summary>
    Task AddBanAsync(AccountBan ban, CancellationToken cancellationToken);

    /// <summary>The case with its identifier, when it exists.</summary>
    Task<Dispute?> FindDisputeAsync(Guid disputeId, CancellationToken cancellationToken);

    /// <summary>The case, locked for the rest of the transaction.</summary>
    Task<Dispute?> LockDisputeAsync(Guid disputeId, CancellationToken cancellationToken);

    /// <summary>
    /// The open case of a submission. Null when the submission has no open
    /// case, so a second dispute of the same seat is refused.
    /// </summary>
    Task<Dispute?> FindOpenDisputeBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>Every case, the newest first.</summary>
    Task<IReadOnlyList<Dispute>> ListDisputesAsync(CancellationToken cancellationToken);

    /// <summary>Writes a new case.</summary>
    Task AddDisputeAsync(Dispute dispute, CancellationToken cancellationToken);

    /// <summary>Writes one piece of evidence of a case.</summary>
    Task AddEvidenceAsync(DisputeEvidence evidence, CancellationToken cancellationToken);

    /// <summary>The evidence of a case, the oldest first.</summary>
    Task<IReadOnlyList<DisputeEvidence>> ListEvidenceAsync(Guid disputeId, CancellationToken cancellationToken);

    /// <summary>Writes the assignment of a case to a moderator.</summary>
    Task AddAssignmentAsync(ModeratorAssignment assignment, CancellationToken cancellationToken);

    /// <summary>The assignments of a case, the oldest first.</summary>
    Task<IReadOnlyList<ModeratorAssignment>> ListAssignmentsAsync(
        Guid disputeId,
        CancellationToken cancellationToken);

    /// <summary>Writes one line of the trail of the module.</summary>
    Task AddAuditAsync(ModerationAudit audit, CancellationToken cancellationToken);

    /// <summary>The trail of a case, the oldest first.</summary>
    Task<IReadOnlyList<ModerationAudit>> ListAuditAsync(Guid disputeId, CancellationToken cancellationToken);

    /// <summary>The trail of a report, the oldest first.</summary>
    Task<IReadOnlyList<ModerationAudit>> ListAuditForReportAsync(
        Guid reportId,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the account may act on the case. The store reads the project
    /// memberships, the task, the participation and the trail of the case, so
    /// the conflict rule is decided in one place and never in a caller. The
    /// account may not be the publisher of the task, the contributor of the
    /// seat, a member of the project of the task, the reporter of the
    /// underlying report or a moderator that already acted on the case.
    /// </summary>
    Task<bool> CanModerateAsync(Guid disputeId, Guid moderatorId, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts of the moderation staff that may receive the case. A
    /// moderator that already acted on the case is never part of the answer,
    /// so the appeal of a case reaches another moderator.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListEligibleModeratorsAsync(
        Guid disputeId,
        CancellationToken cancellationToken);

    /// <summary>Saves the changed rows of the module.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
