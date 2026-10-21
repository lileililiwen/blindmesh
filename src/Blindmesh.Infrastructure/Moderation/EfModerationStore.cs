using Blindmesh.Application.Moderation;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Moderation;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Moderation;

/// <summary>
/// The persistence of the moderation module. The store is scoped, so it shares
/// the scoped database context and the transaction of the request with the
/// escrow primitives of the credit module. The conflict rule of a moderator is
/// decided here, so it is read in one place and never in a caller.
/// </summary>
public sealed class EfModerationStore(BlindmeshDbContext dbContext) : IModerationStore
{
    /// <summary>The actions of the trail that mean that a moderator acted.</summary>
    private static readonly string[] ActingActions =
    [
        "dispute.resolved",
        "dispute.evidence_requested",
        "dispute.closed",
    ];

    public Task<Report?> FindReportAsync(Guid reportId, CancellationToken cancellationToken) =>
        dbContext.Reports.FirstOrDefaultAsync(
            report => report.Id == reportId,
            cancellationToken);

    public async Task<Report?> LockReportAsync(Guid reportId, CancellationToken cancellationToken)
    {
        if (await LockRowAsync("reports", reportId, cancellationToken) is null)
        {
            return null;
        }

        return await dbContext.Reports.FirstOrDefaultAsync(
            report => report.Id == reportId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> ListReportsAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Reports
            .AsNoTracking()
            .OrderByDescending(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task AddReportAsync(Report report, CancellationToken cancellationToken) =>
        dbContext.Reports.AddAsync(report, cancellationToken).AsTask();

    public Task<AccountBan?> FindBanAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.AccountBans.FirstOrDefaultAsync(
            ban => ban.UserId == userId,
            cancellationToken);

    public Task AddBanAsync(AccountBan ban, CancellationToken cancellationToken) =>
        dbContext.AccountBans.AddAsync(ban, cancellationToken).AsTask();

    public Task<Dispute?> FindDisputeAsync(Guid disputeId, CancellationToken cancellationToken) =>
        dbContext.Disputes.FirstOrDefaultAsync(
            dispute => dispute.Id == disputeId,
            cancellationToken);

    public async Task<Dispute?> LockDisputeAsync(Guid disputeId, CancellationToken cancellationToken)
    {
        if (await LockRowAsync("disputes", disputeId, cancellationToken) is null)
        {
            return null;
        }

        return await dbContext.Disputes.FirstOrDefaultAsync(
            dispute => dispute.Id == disputeId,
            cancellationToken);
    }

    public Task<Dispute?> FindOpenDisputeBySubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken) =>
        dbContext.Disputes
            .Where(dispute => dispute.SubmissionId == submissionId
                && (dispute.State == DisputeState.Open
                    || dispute.State == DisputeState.EvidenceRequested))
            .OrderBy(dispute => dispute.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Dispute>> ListDisputesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Disputes
            .AsNoTracking()
            .OrderByDescending(dispute => dispute.CreatedAt)
            .ThenBy(dispute => dispute.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task AddDisputeAsync(Dispute dispute, CancellationToken cancellationToken) =>
        dbContext.Disputes.AddAsync(dispute, cancellationToken).AsTask();

    public Task AddEvidenceAsync(DisputeEvidence evidence, CancellationToken cancellationToken) =>
        dbContext.DisputeEvidence.AddAsync(evidence, cancellationToken).AsTask();

    public async Task<IReadOnlyList<DisputeEvidence>> ListEvidenceAsync(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.DisputeEvidence
            .AsNoTracking()
            .Where(evidence => evidence.DisputeId == disputeId)
            .OrderBy(evidence => evidence.CreatedAt)
            .ThenBy(evidence => evidence.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task AddAssignmentAsync(ModeratorAssignment assignment, CancellationToken cancellationToken) =>
        dbContext.ModeratorAssignments.AddAsync(assignment, cancellationToken).AsTask();

    public async Task<IReadOnlyList<ModeratorAssignment>> ListAssignmentsAsync(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ModeratorAssignments
            .AsNoTracking()
            .Where(assignment => assignment.DisputeId == disputeId)
            .OrderBy(assignment => assignment.AssignedAt)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task AddAuditAsync(ModerationAudit audit, CancellationToken cancellationToken) =>
        dbContext.ModerationAudit.AddAsync(audit, cancellationToken).AsTask();

    public async Task<IReadOnlyList<ModerationAudit>> ListAuditAsync(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ModerationAudit
            .AsNoTracking()
            .Where(audit => audit.DisputeId == disputeId)
            .OrderBy(audit => audit.CreatedAt)
            .ThenBy(audit => audit.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<ModerationAudit>> ListAuditForReportAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ModerationAudit
            .AsNoTracking()
            .Where(audit => audit.ReportId == reportId)
            .OrderBy(audit => audit.CreatedAt)
            .ThenBy(audit => audit.Id)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<bool> CanModerateAsync(
        Guid disputeId,
        Guid moderatorId,
        CancellationToken cancellationToken)
    {
        var dispute = await dbContext.Disputes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            return false;
        }

        // The publisher of the task and the contributor of the seat are never
        // neutral.
        if (moderatorId == dispute.PublisherId || moderatorId == dispute.ContributorId)
        {
            return false;
        }

        // A member of the project of the task is never neutral, and the owner
        // of the project is a member as well.
        var isMember = await dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.ProjectId == dispute.ProjectId && member.UserId == moderatorId,
                cancellationToken);

        if (isMember)
        {
            return false;
        }

        var ownerId = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == dispute.ProjectId)
            .Select(project => (Guid?)project.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId == moderatorId)
        {
            return false;
        }

        // The reporter of the underlying report is never neutral.
        if (dispute.ReportId is Guid reportId)
        {
            var reporterId = await dbContext.Reports
                .AsNoTracking()
                .Where(report => report.Id == reportId)
                .Select(report => (Guid?)report.ReporterId)
                .FirstOrDefaultAsync(cancellationToken);

            if (reporterId == moderatorId)
            {
                return false;
            }
        }

        // A moderator that already acted on the case never receives it again.
        return !await HasActedAsync(dispute.Id, moderatorId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListEligibleModeratorsAsync(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var dispute = await dbContext.Disputes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            return [];
        }

        var staff = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Moderator || user.Role == UserRole.Administrator)
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var eligible = new List<Guid>();

        foreach (var moderatorId in staff)
        {
            if (await CanModerateAsync(dispute.Id, moderatorId, cancellationToken))
            {
                eligible.Add(moderatorId);
            }
        }

        return eligible;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken) > 0;

    private async Task<bool> HasActedAsync(Guid disputeId, Guid moderatorId, CancellationToken cancellationToken) =>
        await dbContext.ModerationAudit
            .AsNoTracking()
            .AnyAsync(
                audit => audit.DisputeId == disputeId
                    && audit.ActorId == moderatorId
                    && ActingActions.Contains(audit.Action),
                cancellationToken);

    private async Task<Guid?> LockRowAsync(string table, Guid id, CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A moderation command must run inside the transaction of the caller.");

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
