using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Findings;

/// <summary>
/// The seam of the findings module for the moderation module. It owns the
/// states of a seat and nothing else: the decision when a seat is disputed and
/// when it ends belongs to the moderation module.
/// </summary>
public sealed class FindingsDisputeSeam(
    IFindingsStore store,
    ITaskStore tasks,
    IParticipationStore participations,
    ParticipationService participationService,
    IClock clock) : IFindingsDisputeSeam
{
    public async Task<DisputeSeatLock?> LockSeatAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var known = await store.FindSubmissionAsync(submissionId, cancellationToken);

        if (known is null)
        {
            return null;
        }

        // The order of the shared lock contract: the task, then the
        // participation and the slot, then the submission.
        var task = await tasks.LockTaskAsync(known.TaskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        var participation = await participations.LockParticipationAsync(known.ParticipationId, cancellationToken);

        if (participation is null)
        {
            return null;
        }

        var slot = participation.SlotId is Guid slotId
            ? await tasks.LockSlotAsync(slotId, cancellationToken)
            : null;

        var submission = await store.LockSubmissionAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return null;
        }

        return new DisputeSeatLock(submission, task, participation, slot);
    }

    public async Task MarkDisputedAsync(
        DisputeSeatLock seat,
        string reason,
        Guid changedBy,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var submission = seat.Submission;
        var participation = seat.Participation;
        var previousState = submission.State;
        var previousParticipationState = participation.State;

        submission.State = SubmissionState.Disputed;
        submission.DisputeReason = reason;
        submission.ReviewedAt = now;
        submission.CorrectionDeadline = null;
        submission.UpdatedAt = now;
        submission.Version += 1;

        await store.AddStatusChangeAsync(new SubmissionStatusChange
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            FromStatus = StateText(previousState),
            ToStatus = StateText(SubmissionState.Disputed),
            ChangedBy = changedBy,
            ChangedAt = now,
        }, cancellationToken);

        participation.State = ParticipationState.Disputed;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await participations.AddStatusChangeAsync(new ParticipationStatusChange
        {
            Id = Guid.NewGuid(),
            ParticipationId = participation.Id,
            TaskId = participation.TaskId,
            FromStatus = ParticipationService.ParticipationStateText(previousParticipationState),
            ToStatus = ParticipationService.ParticipationStateText(ParticipationState.Disputed),
            ChangedBy = changedBy,
            ChangedAt = now,
        }, cancellationToken);

        if (seat.Slot is not null)
        {
            seat.Slot.State = TaskSlotState.Disputed;
        }

        await store.SaveChangesAsync(cancellationToken);
        await participationService.RecomputeTaskStatusAsync(seat.Task, cancellationToken);
    }

    public async Task EndDisputeAsync(
        DisputeSeatLock seat,
        bool accepted,
        Guid changedBy,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var submission = seat.Submission;
        var participation = seat.Participation;
        var previousState = submission.State;
        var previousParticipationState = participation.State;

        // The seat is over. The evidence of the seat stays readable, and the
        // lease of the slot is released, so the task may take another
        // contributor for the slot.
        submission.State = accepted ? SubmissionState.Accepted : SubmissionState.Submitted;
        submission.UpdatedAt = now;
        submission.Version += 1;

        await store.AddStatusChangeAsync(new SubmissionStatusChange
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            FromStatus = StateText(previousState),
            ToStatus = StateText(submission.State),
            ChangedBy = changedBy,
            ChangedAt = now,
        }, cancellationToken);

        participation.State = accepted ? ParticipationState.Approved : ParticipationState.Declined;
        participation.EndedAt = now;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await participations.AddStatusChangeAsync(new ParticipationStatusChange
        {
            Id = Guid.NewGuid(),
            ParticipationId = participation.Id,
            TaskId = participation.TaskId,
            FromStatus = ParticipationService.ParticipationStateText(previousParticipationState),
            ToStatus = ParticipationService.ParticipationStateText(participation.State),
            ChangedBy = changedBy,
            ChangedAt = now,
        }, cancellationToken);

        if (seat.Slot is not null)
        {
            seat.Slot.State = accepted ? TaskSlotState.Settled : TaskSlotState.Available;

            if (!accepted)
            {
                seat.Slot.ContributorId = null;
                seat.Slot.StartedAt = null;
            }
        }

        if (participation.LeaseId is Guid leaseId)
        {
            await participations.ReleaseLeaseAsync(leaseId, now, cancellationToken);
            participation.LeaseId = null;
        }

        await store.SaveChangesAsync(cancellationToken);
        await participationService.RecomputeTaskStatusAsync(seat.Task, cancellationToken);
    }

    private static string StateText(SubmissionState state) => state switch
    {
        SubmissionState.Draft => "draft",
        SubmissionState.Submitted => "submitted",
        SubmissionState.ChangesRequested => "changes_requested",
        SubmissionState.Accepted => "accepted",
        SubmissionState.Disputed => "disputed",
        _ => "unknown",
    };
}
