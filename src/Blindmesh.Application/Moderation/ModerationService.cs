using System.Text.Json;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Credits;
using Blindmesh.Application.Findings;
using Blindmesh.Application.Identity;
using Blindmesh.Application.Projects;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Moderation;
using Blindmesh.Domain.Shared;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Moderation;

/// <summary>
/// The outbox event types of the moderation module. They are written inside the
/// transaction of the case, so the later package that consumes them never
/// misses a decision and never sees an event of a case that was rolled back.
/// </summary>
public static class ModerationEventTypes
{
    public const string ReportOpened = "moderation.report.opened";

    public const string DisputeOpened = "moderation.dispute.opened";

    public const string DisputeResolved = "moderation.dispute.resolved";

    public const string DisputeAppealed = "moderation.dispute.appealed";

    public const string ArbitrationCompensated = "moderation.arbitration.compensated";

    public const string AccountBanned = "moderation.account.banned";
}

/// <summary>
/// The service of the moderation module. It owns the reports and the cases of
/// the arbitration. It never writes a ledger row: the escrow of a case moves
/// through the credit module, inside the transaction of the caller.
/// </summary>
public sealed class ModerationService(
    IModerationStore store,
    IFindingsDisputeSeam seam,
    ICreditService credits,
    IIdentityStore identity,
    ProjectService projects,
    IOutboxWriter outbox,
    IClock clock)
{
    /// <summary>
    /// Writes a report. The reporter reads a receipt back and never learns
    /// anything about another report or about the identity of a reporter.
    /// </summary>
    public async Task<ModerationCommandResult> OpenReportAsync(
        ModerationActor actor,
        OpenReportInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in account is required to write a report.");
        }

        var reason = input.Reason?.Trim() ?? string.Empty;

        if (!IsValidReason(reason))
        {
            return InvalidReason();
        }

        var now = clock.UtcNow;
        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = actor.UserId!.Value,
            Receipt = ReceiptOf(Guid.NewGuid()),
            Reason = reason,
            SubjectType = string.IsNullOrWhiteSpace(input.SubjectType)
                ? "submission"
                : input.SubjectType.Trim(),
            SubjectId = input.SubjectId,
            State = ReportState.Open,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await store.AddReportAsync(report, cancellationToken);

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            ActorId = report.ReporterId,
            Action = "report.opened",
            Reason = "The report was written and waits for a moderator.",
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = ModerationEventTypes.ReportOpened,
            Version = 1,
            ActorId = report.ReporterId,
            AggregateId = report.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new ModerationEventPayload(
                null,
                report.Id,
                report.SubjectId,
                null,
                null,
                null,
                now)),
        }, cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Created,
            Receipt: new ReportReceiptView(report.Id, report.Receipt, ReportStateText(report.State), now));
    }

    /// <summary>
    /// Reads the receipt of a report. Only the reporter and the moderation
    /// staff may read it, and the body of the report never leaves the
    /// moderation module.
    /// </summary>
    public async Task<ModerationCommandResult> GetReportAsync(
        ModerationActor actor,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in account is required to read a report.");
        }

        var report = await store.FindReportAsync(reportId, cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        if (actor.IsStaff)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.Succeeded,
                Report: BuildReportView(report));
        }

        if (report.ReporterId != actor.UserId!.Value)
        {
            return NotFound();
        }

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Receipt: new ReportReceiptView(
                report.Id,
                report.Receipt,
                ReportStateText(report.State),
                report.CreatedAt));
    }

    /// <summary>The reports of the moderation staff.</summary>
    public async Task<ModerationCommandResult> ListReportsAsync(
        ModerationActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsStaff)
        {
            return Forbidden("Only the moderation staff may read the reports.");
        }

        var reports = await store.ListReportsAsync(cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Reports: reports.Select(BuildReportView).ToList());
    }

    /// <summary>
    /// Opens the dispute of a seat. The reason is checked, the states of the
    /// seat are flipped through the findings seam and the case is written with
    /// its trail, all in the transaction of the caller.
    /// </summary>
    public async Task<ModerationCommandResult> OpenDisputeAsync(
        ModerationActor actor,
        Guid submissionId,
        OpenDisputeInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in owner is required to open a dispute.");
        }

        var reason = input.Reason?.Trim() ?? string.Empty;

        if (!IsValidReason(reason))
        {
            return InvalidReason();
        }

        var seat = await seam.LockSeatAsync(submissionId, cancellationToken);

        if (seat is null)
        {
            return NotFound();
        }

        var task = seat.Task;
        var submission = seat.Submission;
        var participation = seat.Participation;

        if (submission.State is not (SubmissionState.Submitted or SubmissionState.ChangesRequested))
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The evidence of the seat cannot be disputed in its current state.");
        }

        if (participation.State is not (
            ParticipationState.Running
            or ParticipationState.Submitted
            or ParticipationState.ChangesRequested))
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The seat of the submission cannot be disputed in its current state.");
        }

        if (!await MayOpenDispute(actor, task, cancellationToken))
        {
            return Forbidden("Only the owner of the task may open a dispute of its evidence.");
        }

        // One active dispute per submission. The case that is open keeps the
        // escrow, so a second dispute never competes with the first one.
        var open = await store.FindOpenDisputeBySubmissionAsync(submissionId, cancellationToken);

        if (open is not null)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The submission already carries an open dispute.");
        }

        if (input.ExpectedVersion is int expected && expected != submission.Version)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.VersionConflict,
                CurrentVersion: submission.Version,
                Message: "The evidence changed since it was read.");
        }

        var now = clock.UtcNow;

        await seam.MarkDisputedAsync(seat, reason, actor.UserId!.Value, cancellationToken);

        var report = await FindOpenReportAsync(submissionId, cancellationToken);

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            ParticipationId = participation.Id,
            TaskId = task.Id,
            ProjectId = task.ProjectId,
            PublisherId = task.OwnerId,
            ContributorId = participation.ContributorId,
            ReportId = report?.Id,
            State = DisputeState.Open,
            Reason = reason,
            BaseReward = task.BaseReward,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await store.AddDisputeAsync(dispute, cancellationToken);

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            ActorId = actor.UserId!.Value,
            Action = "dispute.opened",
            Reason = reason,
            Details = $"The escrow of the seat waits for the arbitration: {dispute.BaseReward} credits.",
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = ModerationEventTypes.DisputeOpened,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = dispute.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new ModerationEventPayload(
                dispute.Id,
                null,
                dispute.SubmissionId,
                dispute.TaskId,
                dispute.PublisherId,
                dispute.ContributorId,
                now)),
        }, cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Created,
            Dispute: BuildDisputeView(dispute));
    }

    /// <summary>
    /// Reads the case of an arbitration. The parties and the moderation staff
    /// read it, and nobody else finds it.
    /// </summary>
    public async Task<ModerationCommandResult> GetDisputeAsync(
        ModerationActor actor,
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in account is required to read a case.");
        }

        var dispute = await store.FindDisputeAsync(disputeId, cancellationToken);

        if (dispute is null)
        {
            return NotFound();
        }

        if (!MaySeeDispute(actor, dispute))
        {
            return NotFound();
        }

        var evidence = await store.ListEvidenceAsync(dispute.Id, cancellationToken);
        var audit = await store.ListAuditAsync(dispute.Id, cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Dispute: BuildDisputeView(dispute),
            Evidence: evidence
                .Select(item => new DisputeEvidenceView(item.Id, item.Kind, item.Note, item.CreatedAt))
                .ToList(),
            Audit: audit
                .Select(item => new ModerationAuditView(
                    item.Id,
                    item.Action,
                    item.Reason,
                    item.Details,
                    item.CreatedAt))
                .ToList());
    }

    /// <summary>The cases of the moderation staff.</summary>
    public async Task<ModerationCommandResult> ListDisputesAsync(
        ModerationActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsStaff)
        {
            return Forbidden("Only the moderation staff may read the cases.");
        }

        var disputes = await store.ListDisputesAsync(cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Disputes: disputes
                .Select(dispute => new DisputeSummaryView(
                    dispute.Id,
                    DisputeStateText(dispute.State),
                    dispute.SubmissionId,
                    dispute.TaskId,
                    dispute.BaseReward,
                    dispute.AwardToContributor,
                    dispute.CreatedAt,
                    dispute.ResolvedAt))
                .ToList());
    }

    /// <summary>
    /// Asks the parties of a case for more evidence. The escrow of the case
    /// stays exactly as it was.
    /// </summary>
    public async Task<ModerationCommandResult> RequestEvidenceAsync(
        ModerationActor actor,
        Guid disputeId,
        RequestEvidenceInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsStaff)
        {
            return Forbidden("Only the moderation staff may ask for evidence.");
        }

        var note = input.Note?.Trim() ?? string.Empty;

        if (!IsValidReason(note))
        {
            return InvalidReason("note");
        }

        var dispute = await store.LockDisputeAsync(disputeId, cancellationToken);

        if (dispute is null)
        {
            return NotFound();
        }

        if (!(await store.CanModerateAsync(dispute.Id, actor.UserId!.Value, cancellationToken)))
        {
            return Forbidden("The account may not act on this case.");
        }

        if (dispute.State is not (DisputeState.Open or DisputeState.EvidenceRequested))
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The case does not wait for evidence any more.");
        }

        if (input.ExpectedVersion is int expected && expected != dispute.Version)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.VersionConflict,
                CurrentVersion: dispute.Version,
                Message: "The case changed since it was read.");
        }

        var now = clock.UtcNow;

        await store.AddEvidenceAsync(new DisputeEvidence
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            SubmittedBy = actor.UserId!.Value,
            Kind = "request",
            Note = note,
            CreatedAt = now,
        }, cancellationToken);

        dispute.State = DisputeState.EvidenceRequested;
        dispute.EvidenceNote = note;
        dispute.UpdatedAt = now;
        dispute.Version += 1;

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            ActorId = actor.UserId!.Value,
            Action = "dispute.evidence_requested",
            Reason = note,
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Dispute: BuildDisputeView(dispute));
    }

    /// <summary>
    /// Decides a case and moves the escrow of the seat. The credit module
    /// writes the ledger; the case keeps the identifier of the transaction, so
    /// the parties read the decision and the ledger result of it.
    /// </summary>
    public async Task<ModerationCommandResult> ResolveDisputeAsync(
        ModerationActor actor,
        Guid disputeId,
        ResolveDisputeInput input,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!actor.IsStaff)
        {
            return Forbidden("Only the moderation staff may decide a case.");
        }

        var reason = input.Reason?.Trim() ?? string.Empty;

        if (!IsValidReason(reason))
        {
            return InvalidReason();
        }

        var dispute = await store.LockDisputeAsync(disputeId, cancellationToken);

        if (dispute is null)
        {
            return NotFound();
        }

        if (!(await store.CanModerateAsync(dispute.Id, actor.UserId!.Value, cancellationToken)))
        {
            return Forbidden("The account may not act on this case.");
        }

        if (dispute.State is not (DisputeState.Open or DisputeState.EvidenceRequested))
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The case was already decided.");
        }

        if (input.ExpectedVersion is int expected && expected != dispute.Version)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.VersionConflict,
                CurrentVersion: dispute.Version,
                Message: "The case changed since it was read.");
        }

        var bonus = input.Bonus;

        if (bonus < 0)
        {
            return InvalidBonus("The bonus may not be negative.");
        }

        if (bonus > 0 && !input.AwardBase)
        {
            return InvalidBonus("A bonus may only be awarded together with the base of the seat.");
        }

        if (bonus > 0 && dispute.AwardToContributor is not null)
        {
            return InvalidBonus("A bonus may only be awarded with the first decision of a case.");
        }

        var now = clock.UtcNow;
        var reference = TaskService.EscrowReference(dispute.TaskId);
        var amount = dispute.BaseReward;

        if (amount <= 0)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.Conflict,
                Message: "The seat of the case holds no credits.");
        }

        var publisher = new CreditActor(dispute.PublisherId, string.Empty);

        // The first decision moved the escrow of the seat. A case that was
        // already decided keeps the money where it is: an appeal that confirms
        // the decision moves nothing, and an appeal that changes it takes the
        // award back with a compensating transaction. The rows of the first
        // decision stay exactly as they were written.
        var previousAward = dispute.AwardToContributor;
        Guid? ledgerTransactionId = dispute.LedgerTransactionId;
        string? ledgerTransactionKind = dispute.LedgerTransactionKind;

        // The amount the first decision awarded to the contributor: the base of
        // the seat and the bonus of the decision. The appeal compensates exactly
        // this amount, so the base and the bonus go back to the publisher
        // together. A case that was decided before the column existed holds
        // zero there, and the base of the seat was its award.
        var awardedAmount = dispute.AwardedAmount > 0 ? dispute.AwardedAmount : dispute.BaseReward;

        if (previousAward is bool firstAward && firstAward != input.AwardBase)
        {
            if (input.AwardBase)
            {
                // The first decision paid the escrow to the publisher. The
                // credit module never takes credits back from the ordinary
                // balance of a publisher, so the case keeps the first decision.
                await store.AddAuditAsync(new ModerationAudit
                {
                    Id = Guid.NewGuid(),
                    DisputeId = dispute.Id,
                    ActorId = actor.UserId!.Value,
                    Action = "dispute.compensation_refused",
                    Reason = "The award of the first decision was already paid to the publisher and cannot be taken back.",
                    CreatedAt = now,
                }, cancellationToken);

                await store.SaveChangesAsync(cancellationToken);

                return new ModerationCommandResult(
                    ModerationCommandStatus.NotReversible,
                    Message: "The award of the first decision was already paid to the publisher and cannot be taken back.");
            }

            var compensation = await credits.CompensateArbitrationAsync(
                publisher,
                new CreditCompensationCommand(
                    reference,
                    awardedAmount,
                    dispute.ContributorId,
                    dispute.PublisherId,
                    idempotencyKey is null ? null : $"{idempotencyKey}:compensate"),
                cancellationToken);

            if (compensation.Status != CreditCommandStatus.Succeeded)
            {
                await store.AddAuditAsync(new ModerationAudit
                {
                    Id = Guid.NewGuid(),
                    DisputeId = dispute.Id,
                    ActorId = actor.UserId!.Value,
                    Action = "dispute.compensation_refused",
                    Reason = "The award cannot be reversed: the balance of the contributor was already spent.",
                    CreatedAt = now,
                }, cancellationToken);

                await store.SaveChangesAsync(cancellationToken);

                return new ModerationCommandResult(
                    ModerationCommandStatus.NotReversible,
                    Message: "The award cannot be reversed: the balance of the contributor was already spent.");
            }

            await store.AddAuditAsync(new ModerationAudit
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                ActorId = actor.UserId!.Value,
                Action = "dispute.compensated",
                Reason = "The award of the first decision was taken back from the contributor.",
                Details = compensation.Transaction?.Id.ToString(),
                CreatedAt = now,
            }, cancellationToken);

            await outbox.EnqueueAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = ModerationEventTypes.ArbitrationCompensated,
                Version = 1,
                ActorId = actor.UserId,
                AggregateId = dispute.Id,
                OccurredAt = now,
                Payload = JsonSerializer.Serialize(new ModerationEventPayload(
                    dispute.Id,
                    null,
                    dispute.SubmissionId,
                    dispute.TaskId,
                    dispute.PublisherId,
                    dispute.ContributorId,
                    now)),
            }, cancellationToken);

            ledgerTransactionId = compensation.Transaction?.Id;
            ledgerTransactionKind = ModerationLedgerKinds.Compensation;
        }
        else if (previousAward is null)
        {
            if (bonus > 0)
            {
                // The bonus leaves the reserved escrow of the publisher and lands
                // in the pending bucket of the contributor. The credit module
                // refuses the command when the escrow of the task does not hold
                // that many credits, so the cap of the funded escrow is the rule
                // of the credit module and not of the case.
                var pending = await credits.BeginPendingAsync(
                    publisher,
                    new CreditPendingCommand(
                        reference,
                        dispute.ContributorId,
                        bonus,
                        idempotencyKey is null ? null : $"{idempotencyKey}:bonus"),
                    cancellationToken);

                if (pending.Status != CreditCommandStatus.Succeeded)
                {
                    return new ModerationCommandResult(
                        ModerationCommandStatus.Conflict,
                        Message: pending.Message ?? "The escrow of the task does not hold that many credits.");
                }
            }

            var arbitration = await credits.ArbitrateAsync(
                publisher,
                new CreditArbitrationCommand(
                    reference,
                    amount + bonus,
                    dispute.ContributorId,
                    dispute.PublisherId,
                    input.AwardBase,
                    idempotencyKey),
                cancellationToken);

            if (arbitration.Status != CreditCommandStatus.Succeeded)
            {
                return new ModerationCommandResult(
                    ModerationCommandStatus.Conflict,
                    Message: arbitration.Message ?? "The escrow of the seat could not be moved.");
            }

            // The row remembers what the decision awarded, so that a later
            // appeal compensates exactly that amount.
            dispute.AwardedAmount = input.AwardBase ? amount + bonus : 0;

            ledgerTransactionId = arbitration.Transaction?.Id;
            ledgerTransactionKind = ModerationLedgerKinds.Arbitration;

            // The seat is over: the participation leaves the disputed state and
            // the lease of the slot is released, so the task may take another
            // contributor for the slot. A case that was already decided keeps
            // the seat as it was, because the appeal only concerns the money.
            var seat = await seam.LockSeatAsync(dispute.SubmissionId, cancellationToken);

            if (seat is not null && seat.Participation.State == ParticipationState.Disputed)
            {
                await seam.EndDisputeAsync(seat, input.AwardBase, actor.UserId!.Value, cancellationToken);
            }
        }

        dispute.State = DisputeState.Resolved;
        dispute.AwardToContributor = input.AwardBase;
        dispute.ResolutionReason = reason;
        dispute.ResolvedBy = actor.UserId;
        dispute.ResolvedAt = now;
        dispute.LedgerTransactionId = ledgerTransactionId;
        dispute.LedgerTransactionKind = ledgerTransactionKind;
        dispute.UpdatedAt = now;
        dispute.Version += 1;

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            ActorId = actor.UserId!.Value,
            Action = "dispute.resolved",
            Reason = reason,
            Details = ledgerTransactionId?.ToString(),
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = ModerationEventTypes.DisputeResolved,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = dispute.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new ModerationEventPayload(
                dispute.Id,
                null,
                dispute.SubmissionId,
                dispute.TaskId,
                dispute.PublisherId,
                dispute.ContributorId,
                now)),
        }, cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Dispute: BuildDisputeView(dispute));
    }

    /// <summary>
    /// Closes a case. The case may only be closed when the decision stands and
    /// the window of the appeal has passed.
    /// </summary>
    public async Task<ModerationCommandResult> CloseDisputeAsync(
        ModerationActor actor,
        Guid disputeId,
        CloseDisputeInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsStaff)
        {
            return Forbidden("Only the moderation staff may close a case.");
        }

        var dispute = await store.LockDisputeAsync(disputeId, cancellationToken);

        if (dispute is null)
        {
            return NotFound();
        }

        if (!(await store.CanModerateAsync(dispute.Id, actor.UserId!.Value, cancellationToken)))
        {
            return Forbidden("The account may not act on this case.");
        }

        if (dispute.State is not DisputeState.Resolved)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The case must be decided before it is closed.");
        }

        if (input.ExpectedVersion is int expected && expected != dispute.Version)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.VersionConflict,
                CurrentVersion: dispute.Version,
                Message: "The case changed since it was read.");
        }

        var now = clock.UtcNow;

        if (dispute.AppealedBy is null
            && dispute.ResolvedAt is DateTimeOffset resolvedAt
            && now < ModerationRules.AppealDeadline(resolvedAt))
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "The window of the appeal is still open.");
        }

        dispute.State = DisputeState.Closed;
        dispute.ClosedBy = actor.UserId;
        dispute.ClosedAt = now;
        dispute.UpdatedAt = now;
        dispute.Version += 1;

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            ActorId = actor.UserId!.Value,
            Action = "dispute.closed",
            Reason = input.Reason?.Trim() ?? "The case is over.",
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Dispute: BuildDisputeView(dispute));
    }

    /// <summary>
    /// Appeals a decision. The appeal is possible once, within seven days of
    /// the decision and only by a party of the case. It never changes the
    /// money: a different moderator decides the case again.
    /// </summary>
    public async Task<ModerationCommandResult> AppealDisputeAsync(
        ModerationActor actor,
        Guid disputeId,
        AppealDisputeInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in party is required to appeal a decision.");
        }

        var reason = input.Reason?.Trim() ?? string.Empty;

        if (!IsValidReason(reason))
        {
            return InvalidReason();
        }

        var dispute = await store.LockDisputeAsync(disputeId, cancellationToken);

        if (dispute is null)
        {
            return NotFound();
        }

        if (actor.UserId!.Value != dispute.PublisherId && actor.UserId.Value != dispute.ContributorId)
        {
            return Forbidden("Only a party of the case may appeal the decision.");
        }

        if (dispute.AppealedBy is not null)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.InvalidTransition,
                Message: "The case already carries the only appeal it may carry.");
        }

        if (dispute.State is not DisputeState.Resolved)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.StateConflict,
                Message: "Only a decided case may be appealed.");
        }

        if (dispute.ResolvedAt is not DateTimeOffset resolvedAt
            || clock.UtcNow > ModerationRules.AppealDeadline(resolvedAt))
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.InvalidTransition,
                Message: "The window of the appeal has passed.");
        }

        if (input.ExpectedVersion is int expected && expected != dispute.Version)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.VersionConflict,
                CurrentVersion: dispute.Version,
                Message: "The case changed since it was read.");
        }

        var now = clock.UtcNow;

        // The case reopens for another moderator.
        dispute.State = DisputeState.Open;
        dispute.AppealedBy = actor.UserId;
        dispute.AppealedAt = now;
        dispute.AppealReason = reason;
        dispute.UpdatedAt = now;
        dispute.Version += 1;

        // The appeal reaches another moderator. A moderator that already acted
        // on the case is never part of the answer.
        var eligible = await store.ListEligibleModeratorsAsync(dispute.Id, cancellationToken);

        foreach (var moderatorId in eligible)
        {
            await store.AddAssignmentAsync(new ModeratorAssignment
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                ModeratorId = moderatorId,
                Role = "appeal",
                AssignedBy = actor.UserId,
                AssignedAt = now,
            }, cancellationToken);

            break;
        }

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            ActorId = actor.UserId!.Value,
            Action = "dispute.appealed",
            Reason = reason,
            Details = eligible.Count > 0
                ? $"The case was given to another moderator: {eligible[0]:D}."
                : "No other moderator may receive the case.",
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = ModerationEventTypes.DisputeAppealed,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = dispute.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new ModerationEventPayload(
                dispute.Id,
                null,
                dispute.SubmissionId,
                dispute.TaskId,
                dispute.PublisherId,
                dispute.ContributorId,
                now)),
        }, cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Dispute: BuildDisputeView(dispute));
    }

    /// <summary>
    /// Bans an account. The account is refused from the next request on, and
    /// the trail of the module keeps the reason of the ban.
    /// </summary>
    public async Task<ModerationCommandResult> BanUserAsync(
        ModerationActor actor,
        Guid userId,
        BanUserInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAdministrator)
        {
            return Forbidden("Only an administrator may ban an account.");
        }

        if (actor.UserId!.Value == userId)
        {
            return Forbidden("An administrator may not ban the own account.");
        }

        var reason = input.Reason?.Trim() ?? string.Empty;

        if (reason.Length is < ModerationRules.ReasonMinLength or > ModerationRules.BanReasonMaxLength)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["reason"] =
                    [
                        $"The reason must hold {ModerationRules.ReasonMinLength} to {ModerationRules.BanReasonMaxLength} characters.",
                    ],
                },
                Message: $"The reason must hold {ModerationRules.ReasonMinLength} to {ModerationRules.BanReasonMaxLength} characters.");
        }

        if (input.Until is DateTimeOffset until && until <= clock.UtcNow)
        {
            return new ModerationCommandResult(
                ModerationCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["until"] = ["The end of the ban must lie in the future."],
                },
                Message: "The end of the ban must lie in the future.");
        }

        var user = await identity.FindUserAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var now = clock.UtcNow;

        // There is one ban per account. A second ban of the same account
        // overwrites the row of the first one, so an account that was banned
        // and whose ban ended may be banned again.
        var ban = await store.FindBanAsync(userId, cancellationToken);

        if (ban is null)
        {
            ban = new AccountBan
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Version = 1,
                CreatedAt = now,
            };

            await store.AddBanAsync(ban, cancellationToken);
        }
        else
        {
            ban.Version += 1;
        }

        ban.BannedUntil = input.Until;
        ban.BanReason = reason;
        ban.BannedBy = actor.UserId;
        ban.BannedAt = now;
        ban.UpdatedAt = now;

        await store.AddAuditAsync(new ModerationAudit
        {
            Id = Guid.NewGuid(),
            ActorId = actor.UserId!.Value,
            Action = "account.banned",
            Reason = reason,
            Details = userId.ToString(),
            CreatedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);

        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = ModerationEventTypes.AccountBanned,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = userId,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new ModerationEventPayload(
                null,
                null,
                null,
                null,
                null,
                userId,
                now)),
        }, cancellationToken);

        return new ModerationCommandResult(
            ModerationCommandStatus.Succeeded,
            Ban: new BanView(ban.UserId, true, ban.BannedUntil, ban.BanReason, ban.BannedAt));
    }

    private async Task<Report?> FindOpenReportAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var reports = await store.ListReportsAsync(cancellationToken);

        return reports
            .Where(report => report.SubjectType == "submission"
                && report.SubjectId == submissionId
                && report.State is ReportState.Open or ReportState.UnderReview)
            .OrderBy(report => report.CreatedAt)
            .FirstOrDefault();
    }

    private async Task<bool> MayOpenDispute(
        ModerationActor actor,
        MarketplaceTask task,
        CancellationToken cancellationToken)
    {
        if (actor.UserId == Guid.Empty)
        {
            return true;
        }

        if (task.OwnerId == actor.UserId!.Value)
        {
            return true;
        }

        var access = await projects.GetAccessAsync(
            new ProjectActor(actor.UserId, actor.Username),
            task.ProjectId,
            cancellationToken);

        return access is { IsOwner: true };
    }

    private static bool MaySeeDispute(ModerationActor actor, Dispute dispute)
    {
        if (actor.IsStaff)
        {
            return true;
        }

        var userId = actor.UserId!.Value;

        return userId == dispute.PublisherId || userId == dispute.ContributorId;
    }

    private static bool IsValidReason(string reason) =>
        reason.Length is >= ModerationRules.ReasonMinLength and <= ModerationRules.ReasonMaxLength;

    private static string ReceiptOf(Guid id) => $"RPT-{id:N}".Substring(0, 14).ToUpperInvariant();

    private static ReportView BuildReportView(Report report) =>
        new(
            report.Id,
            report.Receipt,
            ReportStateText(report.State),
            report.SubjectType,
            report.SubjectId,
            report.Reason,
            report.Resolution,
            report.CreatedAt,
            report.ReviewedAt);

    private static DisputeView BuildDisputeView(Dispute dispute) =>
        new(
            dispute.Id,
            DisputeStateText(dispute.State),
            dispute.SubmissionId,
            dispute.ParticipationId,
            dispute.TaskId,
            dispute.BaseReward,
            dispute.AwardToContributor,
            dispute.ResolutionReason,
            dispute.ResolvedBy,
            dispute.ResolvedAt,
            dispute.LedgerTransactionId,
            dispute.LedgerTransactionKind,
            LedgerAmountOf(dispute),
            dispute.AppealedBy,
            dispute.AppealedAt,
            dispute.AppealReason,
            dispute.ClosedAt,
            dispute.CreatedAt,
            dispute.Version,
            dispute.Reason,
            dispute.EvidenceNote,
            dispute.AwardToContributor is null ? null : dispute.AwardedAmount);

    /// <summary>
    /// The amount of the last ledger transaction of the case: the award of the
    /// decision with its bonus, the base that went back to the publisher, or
    /// the amount the appeal compensated. A case that was decided before the
    /// column of the awarded amount existed holds zero there, and the base of
    /// the seat was its award.
    /// </summary>
    private static long? LedgerAmountOf(Dispute dispute) =>
        dispute.AwardToContributor is null
            ? null
            : dispute.AwardedAmount > 0
                ? dispute.AwardedAmount
                : dispute.BaseReward;

    private static string ReportStateText(ReportState state) => state switch
    {
        ReportState.Open => "open",
        ReportState.UnderReview => "under_review",
        ReportState.Resolved => "resolved",
        ReportState.Dismissed => "dismissed",
        _ => "unknown",
    };

    private static string DisputeStateText(DisputeState state) => state switch
    {
        DisputeState.Open => "open",
        DisputeState.EvidenceRequested => "evidence_requested",
        DisputeState.Resolved => "resolved",
        DisputeState.Closed => "closed",
        _ => "unknown",
    };

    private static ModerationCommandResult Forbidden(string message) =>
        new(ModerationCommandStatus.Forbidden, Message: message);

    private static ModerationCommandResult NotFound() =>
        new(ModerationCommandStatus.NotFound, Message: "The resource is not there.");

    private static ModerationCommandResult InvalidReason(string field = "reason") =>
        new(
            ModerationCommandStatus.ValidationFailed,
            FieldErrors: new Dictionary<string, string[]>
            {
                [field] =
                [
                    $"The reason must hold {ModerationRules.ReasonMinLength} to {ModerationRules.ReasonMaxLength} characters.",
                ],
            },
            Message: $"The reason must hold {ModerationRules.ReasonMinLength} to {ModerationRules.ReasonMaxLength} characters.");

    /// <summary>
    /// The bonus of a decision is not valid with this command. Nothing is
    /// written when the command is refused.
    /// </summary>
    private static ModerationCommandResult InvalidBonus(string message) =>
        new(
            ModerationCommandStatus.ValidationFailed,
            FieldErrors: new Dictionary<string, string[]>
            {
                ["bonus"] = [message],
            },
            Message: message);
}

/// <summary>The kinds of the ledger transactions of the moderation module.</summary>
public static class ModerationLedgerKinds
{
    public const string Arbitration = "arbitration";

    public const string Compensation = "compensation";
}

/// <summary>The payload of the outbox events of the moderation module.</summary>
public sealed record ModerationEventPayload(
    Guid? DisputeId,
    Guid? ReportId,
    Guid? SubmissionId,
    Guid? TaskId,
    Guid? PublisherId,
    Guid? ContributorId,
    DateTimeOffset OccurredAt);
