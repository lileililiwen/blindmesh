using System.Globalization;
using System.Text.Json;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Capabilities;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Credits;
using Blindmesh.Application.Projects;
using Blindmesh.Domain.Shared;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Tasks;

/// <summary>
/// The service of the participations of a task. It owns the application, the
/// acceptance, the start and the withdrawal of one seat, the deadlines of the
/// architecture and the escrow that belongs to the seat. The actor policy is
/// checked again on every command, so a change of the membership or of the
/// visibility of the parent project is honoured by the next read.
/// </summary>
public sealed class ParticipationService(
    IParticipationStore store,
    ITaskStore tasks,
    ProjectService projects,
    CapabilityService capabilities,
    ICreditService credits,
    IOutboxWriter outbox,
    IClock clock)
{
    private const int SweepLimit = 50;

    /// <summary>
    /// The seat of one contributor in one slot of a task. The application holds
    /// no slot; the acceptance holds exactly one exclusive lease.
    /// </summary>
    public async Task<ParticipationCommandResult> ApplyAsync(
        ParticipationActor actor,
        Guid taskId,
        ParticipationApplyInput input,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in contributor is required to apply for a seat.");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!input.ScopeConsent)
        {
            errors["scope_consent"] =
            [
                "The scope of the task has to be accepted before a seat is asked for.",
            ];
        }

        if (input.ExpectedVersion is null)
        {
            errors["expected_version"] = ["The version of the task is required."];
        }

        if (errors.Count > 0)
        {
            return new ParticipationCommandResult(
                ParticipationCommandStatus.ValidationFailed,
                FieldErrors: errors);
        }

        var expectedVersion = input.ExpectedVersion!.Value;

        // The lock of the task comes before the lock of the seat, as the shared
        // contract of the lock order demands.
        var task = await tasks.LockTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (access is null || !MaySeeTask(access, task))
        {
            return NotFound();
        }

        // A member of the project of the task never earns from that project,
        // even when the public capabilities match.
        if (access.IsInsider)
        {
            return Forbidden("A member of the project cannot earn credits from it.");
        }

        if (task.Status is not (TaskState.Open or TaskState.InProgress))
        {
            return StateConflict("This task does not accept applications.");
        }

        if (task.PublishedAt is not DateTimeOffset publishedAt)
        {
            return StateConflict("This task is not published.");
        }

        var now = clock.UtcNow;

        if (now > ParticipationRules.ApplyDeadline(publishedAt))
        {
            return StateConflict("The admission of this task closed after 48 hours.");
        }

        if (task.Version != expectedVersion)
        {
            return new ParticipationCommandResult(
                ParticipationCommandStatus.VersionConflict,
                CurrentVersion: task.Version,
                Message: "The task was changed somewhere else.");
        }

        var existing = await store.FindParticipationForContributorAsync(
            taskId,
            actor.UserId!.Value,
            cancellationToken);

        if (existing is not null)
        {
            return StateConflict("This contributor already applied for this task.");
        }

        var evaluation = await EvaluateAsync(actor, taskId, cancellationToken);

        if (evaluation.Status == CapabilityEvaluationStatus.NotFound)
        {
            return NotFound();
        }

        if (evaluation.Status != CapabilityEvaluationStatus.Evaluated)
        {
            return Forbidden(evaluation.Message ?? "The eligibility of this contributor is unknown.");
        }

        if (evaluation.Evaluation is null || !evaluation.Evaluation.Eligible)
        {
            return StateConflict(UnmetConditions(evaluation.Evaluation));
        }

        var participation = new Participation
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ApplicationId = Guid.NewGuid(),
            ContributorId = actor.UserId.Value,
            State = ParticipationState.Applied,
            AppliedAt = now,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var application = new TaskApplication
        {
            Id = participation.ApplicationId,
            TaskId = taskId,
            ContributorId = actor.UserId.Value,
            CapabilitySnapshot = Snapshot(evaluation.Evaluation),
            ScopeConsent = input.ScopeConsent,
            ExpectedVersion = expectedVersion,
            CreatedAt = now,
        };

        await store.AddParticipationAsync(participation, cancellationToken);
        await store.AddApplicationAsync(application, cancellationToken);
        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = taskId,
                FromStatus = ParticipationStateText(ParticipationState.Applied),
                ToStatus = ParticipationStateText(ParticipationState.Applied),
                ChangedBy = actor.UserId.Value,
                ChangedAt = now,
            },
            cancellationToken);

        // The owner of the task is told that a seat was applied for. The event
        // is written inside the transaction of the command, so the application
        // and its event are stored together or not at all.
        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = TasksEventTypes.ApplicationReceived,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = participation.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new TasksEventPayload(
                participation.Id,
                task.Id,
                task.ProjectId,
                task.OwnerId,
                participation.ContributorId,
                now)),
        }, cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var detail = await BuildDetailAsync(actor, participation, task, access, cancellationToken);

        return new ParticipationCommandResult(ParticipationCommandStatus.Created, detail);
    }

    /// <summary>
    /// The owner accepts an application. The acceptance creates exactly one
    /// exclusive lease, checks the eligibility of the applicant again and
    /// recomputes the aggregate status of the task.
    /// </summary>
    public async Task<ParticipationCommandResult> AcceptAsync(
        ParticipationActor actor,
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var (access, failure) = await LoadAsync(actor, participationId, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!access!.CanManage)
        {
            return Forbidden("Only the owner of the task may accept an application.");
        }

        var participation = access.Participation;
        var task = access.Task;

        if (participation.State != ParticipationState.Applied)
        {
            return InvalidTransition("This application was already answered.");
        }

        if (task.Status is not (TaskState.Open or TaskState.InProgress))
        {
            return StateConflict("The admission of this task is closed.");
        }

        var applicant = await projects.GetAccessAsync(
            new ProjectActor(participation.ContributorId, string.Empty),
            task.ProjectId,
            cancellationToken);

        if (applicant is null || applicant.IsInsider)
        {
            return StateConflict("A member of the project cannot earn credits from it.");
        }

        var evaluation = await EvaluateAsync(
            new ParticipationActor(participation.ContributorId, string.Empty),
            task.Id,
            cancellationToken);

        if (evaluation.Status != CapabilityEvaluationStatus.Evaluated)
        {
            return StateConflict("The eligibility of this applicant is unknown.");
        }

        if (evaluation.Evaluation is null || !evaluation.Evaluation.Eligible)
        {
            return StateConflict(UnmetConditions(evaluation.Evaluation));
        }

        var slots = await tasks.ListSlotsAsync([task.Id], cancellationToken);
        var candidate = slots.GetValueOrDefault(task.Id, [])
            .Where(slot => slot.State == TaskSlotState.Available)
            .OrderBy(slot => slot.Position)
            .FirstOrDefault();

        if (candidate is null)
        {
            return StateConflict("Every slot of this task is taken.");
        }

        var slot = await tasks.LockSlotAsync(candidate.Id, cancellationToken);

        if (slot is null || slot.State != TaskSlotState.Available)
        {
            return StateConflict("Every slot of this task is taken.");
        }

        var now = clock.UtcNow;

        var lease = new SlotLease
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            SlotId = slot.Id,
            ParticipationId = participation.Id,
            ContributorId = participation.ContributorId,
            AcquiredAt = now,
        };

        await store.AddLeaseAsync(lease, cancellationToken);

        slot.State = TaskSlotState.Leased;
        slot.ContributorId = participation.ContributorId;

        participation.State = ParticipationState.Accepted;
        participation.AcceptedAt = now;
        participation.SlotId = slot.Id;
        participation.LeaseId = lease.Id;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = task.Id,
                FromStatus = ParticipationStateText(ParticipationState.Applied),
                ToStatus = ParticipationStateText(ParticipationState.Accepted),
                ChangedBy = actor.UserId!.Value,
                ChangedAt = now,
            },
            cancellationToken);

        // The contributor is told that the seat was accepted. The event is
        // written inside the transaction of the command, so the acceptance and
        // its event are stored together or not at all.
        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = TasksEventTypes.ApplicationAccepted,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = participation.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new TasksEventPayload(
                participation.Id,
                task.Id,
                task.ProjectId,
                task.OwnerId,
                participation.ContributorId,
                now)),
        }, cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        await RecomputeTaskStatusAsync(task, cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var detail = await BuildDetailAsync(actor, participation, task, access.ProjectAccess, cancellationToken);

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded, detail);
    }

    /// <summary>The owner declines an application. It never held a slot.</summary>
    public async Task<ParticipationCommandResult> DeclineAsync(
        ParticipationActor actor,
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var (access, failure) = await LoadAsync(actor, participationId, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!access!.CanManage)
        {
            return Forbidden("Only the owner of the task may decline an application.");
        }

        var participation = access.Participation;

        if (participation.State != ParticipationState.Applied)
        {
            return InvalidTransition("This application was already answered.");
        }

        var now = clock.UtcNow;

        participation.State = ParticipationState.Declined;
        participation.EndedAt = now;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = participation.TaskId,
                FromStatus = ParticipationStateText(ParticipationState.Applied),
                ToStatus = ParticipationStateText(ParticipationState.Declined),
                ChangedBy = actor.UserId!.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var detail = await BuildDetailAsync(
            actor,
            participation,
            access.Task,
            access.ProjectAccess,
            cancellationToken);

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded, detail);
    }

    /// <summary>
    /// The contributor starts the work of the seat. The reserved budget of the
    /// slot moves into the pending bucket of the contributor in the same
    /// transaction as the state of the seat.
    /// </summary>
    public async Task<ParticipationCommandResult> StartAsync(
        ParticipationActor actor,
        Guid participationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var (access, failure) = await LoadAsync(actor, participationId, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!access!.IsContributor)
        {
            return Forbidden("Only the contributor of the seat may start the work.");
        }

        var participation = access.Participation;
        var task = access.Task;

        if (participation.State != ParticipationState.Accepted)
        {
            return InvalidTransition("Only an accepted seat may start the work.");
        }

        if (participation.AcceptedAt is not DateTimeOffset acceptedAt)
        {
            return StateConflict("The seat was never accepted.");
        }

        var now = clock.UtcNow;

        if (now > ParticipationRules.StartDeadline(acceptedAt))
        {
            return StateConflict("The start window of this seat closed after 24 hours.");
        }

        if (task.Status == TaskState.Cancelled)
        {
            return StateConflict("This task was cancelled and does not accept work.");
        }

        if (participation.SlotId is not Guid slotId)
        {
            return StateConflict("The seat does not hold a slot.");
        }

        var slot = await tasks.LockSlotAsync(slotId, cancellationToken);

        if (slot is null)
        {
            return StateConflict("The slot of this seat does not exist.");
        }

        // The reserved budget of the slot moves into the pending bucket of the
        // contributor. Only the base reward moves, because the bonus of the
        // slot stays reserved until the settlement of the work.
        var pending = await credits.BeginPendingAsync(
            await PublisherAsync(task, cancellationToken),
            new CreditPendingCommand(
                TaskService.EscrowReference(task.Id),
                participation.ContributorId,
                task.BaseReward,
                idempotencyKey),
            cancellationToken);

        if (pending.Status != CreditCommandStatus.Succeeded)
        {
            return StateConflict(pending.Message ?? "The budget of the seat could not be started.");
        }

        participation.State = ParticipationState.Running;
        participation.StartedAt = now;
        participation.ExecutionDeadline = ParticipationRules.ExecutionDeadline(now, task.Deadline);
        participation.UpdatedAt = now;
        participation.Version += 1;

        slot.State = TaskSlotState.Active;
        slot.StartedAt = now;

        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = task.Id,
                FromStatus = ParticipationStateText(ParticipationState.Accepted),
                ToStatus = ParticipationStateText(ParticipationState.Running),
                ChangedBy = actor.UserId!.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        await RecomputeTaskStatusAsync(task, cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var detail = await BuildDetailAsync(actor, participation, task, access.ProjectAccess, cancellationToken);

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded, detail);
    }

    /// <summary>
    /// The contributor withdraws the seat before the work starts. The lease is
    /// released and the slot is given back to the task.
    /// </summary>
    public async Task<ParticipationCommandResult> WithdrawAsync(
        ParticipationActor actor,
        Guid participationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var (access, failure) = await LoadAsync(actor, participationId, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!access!.IsContributor)
        {
            return Forbidden("Only the contributor of the seat may withdraw it.");
        }

        var participation = access.Participation;
        var task = access.Task;

        if (participation.State is not (ParticipationState.Applied or ParticipationState.Accepted))
        {
            return InvalidTransition("Only a seat that did not start may be withdrawn.");
        }

        var now = clock.UtcNow;
        var previous = participation.State;

        if (participation.State == ParticipationState.Accepted && participation.SlotId is Guid slotId)
        {
            var slot = await tasks.LockSlotAsync(slotId, cancellationToken);

            if (slot is null)
            {
                return StateConflict("The slot of this seat does not exist.");
            }

            var released = await ReleaseSeatAsync(
                task,
                slot,
                participation,
                idempotencyKey,
                cancellationToken);

            if (released is not null)
            {
                return released;
            }
        }

        participation.State = ParticipationState.Withdrawn;
        participation.EndedAt = now;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = task.Id,
                FromStatus = ParticipationStateText(previous),
                ToStatus = ParticipationStateText(ParticipationState.Withdrawn),
                ChangedBy = actor.UserId!.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        await RecomputeTaskStatusAsync(task, cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        var detail = await BuildDetailAsync(actor, participation, task, access.ProjectAccess, cancellationToken);

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded, detail);
    }

    /// <summary>The committed state of one seat.</summary>
    public async Task<ParticipationCommandResult> GetAsync(
        ParticipationActor actor,
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var participation = await store.FindParticipationAsync(participationId, cancellationToken);

        if (participation is null)
        {
            return NotFound();
        }

        var task = await tasks.FindTaskAsync(participation.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        var isContributor = participation.ContributorId == actor.UserId;
        var canManage = task.OwnerId == actor.UserId || access.IsOwner;

        if (!isContributor && !canManage)
        {
            return NotFound();
        }

        var detail = await BuildDetailAsync(actor, participation, task, access, cancellationToken);

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded, detail);
    }

    /// <summary>The seats of the contributor that signed in.</summary>
    public async Task<ParticipationListResult> ListAsync(
        ParticipationActor actor,
        ParticipationListQuery query,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new ParticipationListResult(
                ParticipationCommandStatus.Forbidden,
                Message: "A signed in contributor is required to read the work.");
        }

        var rows = await store.ListAsync(query, actor.UserId, cancellationToken);
        var page = await BuildPageAsync(rows, query, cancellationToken);

        return new ParticipationListResult(ParticipationCommandStatus.Succeeded, page);
    }

    /// <summary>
    /// The applications of one task. Only the owner of the task and the owner
    /// of the project may read them.
    /// </summary>
    public async Task<ParticipationListResult> ListTaskApplicationsAsync(
        ParticipationActor actor,
        Guid taskId,
        ParticipationListQuery query,
        CancellationToken cancellationToken)
    {
        var task = await tasks.FindTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return new ParticipationListResult(
                ParticipationCommandStatus.NotFound,
                Message: "The task does not exist.");
        }

        var access = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (access is null)
        {
            return new ParticipationListResult(
                ParticipationCommandStatus.NotFound,
                Message: "The task does not exist.");
        }

        if (task.OwnerId != actor.UserId && !access.IsOwner)
        {
            return new ParticipationListResult(
                ParticipationCommandStatus.NotFound,
                Message: "The task does not exist.");
        }

        var rows = await store.ListAsync(query with { TaskId = taskId }, null, cancellationToken);
        var page = await BuildPageAsync(rows, query, cancellationToken);

        return new ParticipationListResult(ParticipationCommandStatus.Succeeded, page);
    }

    /// <summary>
    /// The sweep of the deadlines. It expires the applications that wait
    /// longer than 48 hours for a decision and the seats that wait longer than
    /// 24 hours for the start. The sweep uses the injected clock and the lock
    /// order of the commands, so it never races a command into two legal
    /// transitions.
    /// </summary>
    public async Task<ParticipationExpiryReport> ExpireOverdueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var overdue = await store.ListOverdueAsync(now, SweepLimit, cancellationToken);

        var expired = 0;
        var released = 0;

        foreach (var candidate in overdue)
        {
            var participation = await store.LockParticipationAsync(candidate.Id, cancellationToken);

            if (participation is null)
            {
                continue;
            }

            var task = await tasks.LockTaskAsync(participation.TaskId, cancellationToken);

            if (task is null)
            {
                continue;
            }

            if (participation.State == ParticipationState.Applied &&
                now >= ParticipationRules.DecisionDeadline(participation.AppliedAt))
            {
                var result = await ExpireAppliedAsync(participation, task, cancellationToken);

                if (result.Status == ParticipationCommandStatus.Succeeded)
                {
                    expired += 1;
                }

                continue;
            }

            if (participation.State == ParticipationState.Accepted &&
                participation.AcceptedAt is DateTimeOffset acceptedAt &&
                now >= ParticipationRules.StartDeadline(acceptedAt))
            {
                var result = await ExpireAcceptedAsync(participation, task, cancellationToken);

                if (result.Status == ParticipationCommandStatus.Succeeded)
                {
                    expired += 1;
                    released += 1;
                }
            }
        }

        return new ParticipationExpiryReport(expired, released);
    }

    /// <summary>
    /// Expires an application that waited longer than the deadline of a
    /// decision. It never held a slot and never touched the escrow.
    /// </summary>
    private async Task<ParticipationCommandResult> ExpireAppliedAsync(
        Participation participation,
        MarketplaceTask task,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        participation.State = ParticipationState.Expired;
        participation.EndedAt = now;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = task.Id,
                FromStatus = ParticipationStateText(ParticipationState.Applied),
                ToStatus = ParticipationStateText(ParticipationState.Expired),
                ChangedBy = participation.ContributorId,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded);
    }

    /// <summary>
    /// Expires a seat that was accepted but never started. The lease is
    /// released and the slot is given back to the task when the task still
    /// accepts work; otherwise the budget of the slot is refunded to the
    /// publisher.
    /// </summary>
    private async Task<ParticipationCommandResult> ExpireAcceptedAsync(
        Participation participation,
        MarketplaceTask task,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (participation.SlotId is Guid slotId)
        {
            var slot = await tasks.LockSlotAsync(slotId, cancellationToken);

            if (slot is null)
            {
                return StateConflict("The slot of this seat does not exist.");
            }

            var released = await ReleaseSeatAsync(task, slot, participation, null, cancellationToken);

            if (released is not null)
            {
                return released;
            }
        }

        participation.State = ParticipationState.Expired;
        participation.EndedAt = now;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await store.AddStatusChangeAsync(
            new ParticipationStatusChange
            {
                Id = Guid.NewGuid(),
                ParticipationId = participation.Id,
                TaskId = task.Id,
                FromStatus = ParticipationStateText(ParticipationState.Accepted),
                ToStatus = ParticipationStateText(ParticipationState.Expired),
                ChangedBy = participation.ContributorId,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        await RecomputeTaskStatusAsync(task, cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return Conflict();
        }

        return new ParticipationCommandResult(ParticipationCommandStatus.Succeeded);
    }

    /// <summary>
    /// Gives the budget of a seat that never started back to the task. The
    /// budget stays reserved for another contributor while the task still
    /// accepts work and its deadline did not pass; otherwise it is refunded to
    /// the publisher.
    /// </summary>
    private async Task<ParticipationCommandResult?> ReleaseSeatAsync(
        MarketplaceTask task,
        TaskSlot slot,
        Participation participation,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (participation.LeaseId is Guid leaseId)
        {
            await store.ReleaseLeaseAsync(leaseId, clock.UtcNow, cancellationToken);
        }

        var now = clock.UtcNow;
        var reallocatable = task.Status is TaskState.Open or TaskState.InProgress &&
            task.Deadline is DateTimeOffset deadline &&
            deadline > now;

        if (reallocatable)
        {
            // The slot is free again and the budget stays reserved for the
            // next contributor of this task.
            slot.State = TaskSlotState.Available;
            slot.ContributorId = null;

            return null;
        }

        var refund = await credits.RefundUnstartedAsync(
            await PublisherAsync(task, cancellationToken),
            new CreditRefundCommand(
                TaskService.EscrowReference(task.Id),
                slot.Reward,
                idempotencyKey),
            cancellationToken);

        if (refund.Status != CreditCommandStatus.Succeeded)
        {
            return StateConflict(refund.Message ?? "The budget of the seat could not be released.");
        }

        slot.State = TaskSlotState.Refunded;
        slot.ContributorId = null;

        return null;
    }

    /// <summary>
    /// Recomputes the aggregate status of the task under the lock of the task.
    /// A task that carries running work is in progress; a task that waits for
    /// the review of a submission or of a dispute is in review; a task whose
    /// slots are all settled or refunded is completed; a task without running
    /// work is open again. A cancelled task keeps its state.
    /// </summary>
    public async Task RecomputeTaskStatusAsync(MarketplaceTask task, CancellationToken cancellationToken)
    {
        if (task.Status is TaskState.Draft or TaskState.Cancelled)
        {
            return;
        }

        var running = await store.HasRunningParticipationAsync(task.Id, cancellationToken);
        var pending = await store.HasReviewPendingParticipationAsync(task.Id, cancellationToken);
        var slots = await tasks.ListSlotsAsync([task.Id], cancellationToken);
        var slotRows = slots.GetValueOrDefault(task.Id, []);
        var terminal = slotRows.Count > 0 &&
            slotRows.All(slot => slot.State is TaskSlotState.Settled or TaskSlotState.Refunded);

        var next = running
            ? TaskState.InProgress
            : pending
                ? TaskState.Review
                : terminal
                    ? TaskState.Completed
                    : TaskState.Open;

        if (task.Status == next)
        {
            return;
        }

        var from = task.Status;

        task.Status = next;
        task.UpdatedAt = clock.UtcNow;

        await tasks.AddStatusChangeAsync(
            new TaskStatusChange
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                FromStatus = TaskService.StatusText(from),
                ToStatus = TaskService.StatusText(next),
                ChangedBy = task.OwnerId,
                ChangedAt = task.UpdatedAt,
            },
            cancellationToken);
    }

    /// <summary>
    /// Loads the seat with the lock of the task and the lock of the seat. The
    /// order of the locks is the order of the shared contract.
    /// </summary>
    private async Task<(ParticipationAccess? Access, ParticipationCommandResult? Failure)> LoadAsync(
        ParticipationActor actor,
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var found = await store.FindParticipationAsync(participationId, cancellationToken);

        if (found is null)
        {
            return (null, NotFound());
        }

        var task = await tasks.LockTaskAsync(found.TaskId, cancellationToken);

        if (task is null)
        {
            return (null, NotFound());
        }

        var access = await projects.GetAccessAsync(
            ToProjectActor(actor),
            task.ProjectId,
            cancellationToken);

        if (access is null)
        {
            return (null, NotFound());
        }

        var participation = await store.LockParticipationAsync(participationId, cancellationToken);

        if (participation is null)
        {
            return (null, NotFound());
        }

        var isContributor = participation.ContributorId == actor.UserId;
        var canManage = task.OwnerId == actor.UserId || access.IsOwner;

        if (!isContributor && !canManage)
        {
            return (null, NotFound());
        }

        return (new ParticipationAccess(participation, task, access, canManage, isContributor), null);
    }

    private async Task<ParticipationDetailView> BuildDetailAsync(
        ParticipationActor actor,
        Participation participation,
        MarketplaceTask task,
        ProjectAccessView access,
        CancellationToken cancellationToken)
    {
        var slots = await tasks.ListSlotsAsync([task.Id], cancellationToken);
        var slot = slots.GetValueOrDefault(task.Id, [])
            .FirstOrDefault(candidate => candidate.Id == participation.SlotId);

        var steps = await tasks.ListStepsAsync([task.Id], cancellationToken);
        var history = await store.ListHistoryAsync(participation.Id, cancellationToken);
        var usernames = await tasks.ListUsernamesAsync(
            [task.OwnerId, participation.ContributorId],
            cancellationToken);
        var projectNames = await tasks.ListProjectNamesAsync([task.ProjectId], cancellationToken);

        var isContributor = participation.ContributorId == actor.UserId;
        var canManage = task.OwnerId == actor.UserId || access.IsOwner;

        var application = await store.FindApplicationAsync(participation.ApplicationId, cancellationToken);

        // The instructions of the task are released with the seat of the
        // contributor and never before.
        var released = participation.State is ParticipationState.Accepted or ParticipationState.Running;

        var stepViews = steps.GetValueOrDefault(task.Id, [])
            .Select(step => new TaskStepView(step.Position, step.Title, step.ExpectedResult))
            .ToArray();

        return new ParticipationDetailView(
            participation.Id,
            task.Id,
            task.Title,
            task.ProjectId,
            projectNames.GetValueOrDefault(task.ProjectId, string.Empty),
            usernames.GetValueOrDefault(task.OwnerId, string.Empty),
            usernames.GetValueOrDefault(participation.ContributorId, string.Empty),
            ParticipationStateText(participation.State),
            participation.AppliedAt,
            participation.AcceptedAt,
            participation.StartedAt,
            participation.AcceptedAt is DateTimeOffset acceptedAt
                ? ParticipationRules.StartDeadline(acceptedAt)
                : null,
            participation.ExecutionDeadline,
            participation.EndedAt,
            slot?.Reward ?? 0,
            slot is null ? string.Empty : SlotStateText(slot.State),
            application?.ScopeConsent ?? false,
            released,
            released ? task.Scope : null,
            released ? stepViews : [],
            TaskService.ModeNames(task.Modes),
            history
                .Select(change => new ParticipationStatusChangeView(
                    change.FromStatus,
                    change.ToStatus,
                    usernames.GetValueOrDefault(change.ChangedBy, string.Empty),
                    change.ChangedAt))
                .ToArray(),
            canManage && participation.State == ParticipationState.Applied,
            canManage && participation.State == ParticipationState.Applied,
            isContributor && participation.State == ParticipationState.Accepted,
            isContributor && participation.State is ParticipationState.Applied or ParticipationState.Accepted,
            participation.Version);
    }

    private async Task<ParticipationPage> BuildPageAsync(
        ParticipationListRows rows,
        ParticipationListQuery query,
        CancellationToken cancellationToken)
    {
        if (rows.Rows.Count == 0)
        {
            return new ParticipationPage([], null);
        }

        var userIds = rows.Rows
            .SelectMany(row => new[] { row.OwnerId, row.ContributorId })
            .Distinct()
            .ToArray();

        var usernames = await tasks.ListUsernamesAsync(userIds, cancellationToken);
        var projectNames = await tasks.ListProjectNamesAsync(
            rows.Rows.Select(row => row.ProjectId).Distinct().ToArray(),
            cancellationToken);

        var items = rows.Rows
            .Select(row => new ParticipationSummaryView(
                row.Id,
                row.TaskId,
                row.TaskTitle,
                row.ProjectId,
                projectNames.GetValueOrDefault(row.ProjectId, string.Empty),
                usernames.GetValueOrDefault(row.OwnerId, string.Empty),
                usernames.GetValueOrDefault(row.ContributorId, string.Empty),
                ParticipationStateText(row.State),
                row.AppliedAt,
                row.AcceptedAt,
                row.StartedAt,
                row.AcceptedAt is DateTimeOffset acceptedAt
                    ? ParticipationRules.StartDeadline(acceptedAt)
                    : null,
                row.ExecutionDeadline,
                row.SlotReward,
                row.SlotState is TaskSlotState state ? SlotStateText(state) : string.Empty,
                row.Version))
            .ToArray();

        return new ParticipationPage(items, rows.Next);
    }

    private async Task<CapabilityEvaluationResult> EvaluateAsync(
        ParticipationActor actor,
        Guid taskId,
        CancellationToken cancellationToken) =>
        await capabilities.EvaluateForTaskAsync(
            new CapabilityActor(actor.UserId, actor.Username),
            taskId,
            cancellationToken);

    private async Task<CreditActor> PublisherAsync(MarketplaceTask task, CancellationToken cancellationToken)
    {
        var usernames = await tasks.ListUsernamesAsync([task.OwnerId], cancellationToken);

        return new CreditActor(
            task.OwnerId,
            usernames.GetValueOrDefault(task.OwnerId, string.Empty));
    }

    /// <summary>
    /// The snapshot of the evaluation of the hard conditions. It holds the
    /// decision and the reason of every condition and never a credential.
    /// </summary>
    private static string Snapshot(CapabilityEligibilityView? evaluation)
    {
        var payload = new
        {
            eligible = evaluation?.Eligible ?? false,
            matchable_declarations = evaluation?.MatchableDeclarations ?? 0,
            conditions = (evaluation?.Conditions ?? [])
                .Select(condition => new
                {
                    kind = condition.Kind,
                    value = condition.Value,
                    satisfied = condition.Satisfied,
                    reason = condition.Reason,
                })
                .ToArray(),
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// A visitor only reads a task of a public and active project. A task of a
    /// private project or a private task of a public project is not found.
    /// </summary>
    private static bool MaySeeTask(ProjectAccessView access, MarketplaceTask task) =>
        access.IsInsider ||
        (access.IsActive &&
         string.Equals(access.Visibility, "public", StringComparison.Ordinal) &&
         (task.Visibility == TaskVisibility.Public || task.Visibility == TaskVisibility.Matched));

    private static string UnmetConditions(CapabilityEligibilityView? evaluation)
    {
        if (evaluation is null)
        {
            return "The hard conditions of this task are not met.";
        }

        var unmet = evaluation.Conditions
            .Where(condition => !condition.Satisfied)
            .Select(condition => $"{condition.Kind} {condition.Value}")
            .ToArray();

        if (unmet.Length == 0)
        {
            return "The hard conditions of this task are not met.";
        }

        return "The hard conditions of this task are not met: " + string.Join(", ", unmet) + ".";
    }

    private static ProjectActor ToProjectActor(ParticipationActor actor) =>
        new(actor.UserId, actor.Username);

    /// <summary>The name of a state of a participation.</summary>
    public static string ParticipationStateText(ParticipationState state) => state switch
    {
        ParticipationState.Applied => "applied",
        ParticipationState.Accepted => "accepted",
        ParticipationState.Running => "running",
        ParticipationState.Submitted => "submitted",
        ParticipationState.ChangesRequested => "changes_requested",
        ParticipationState.Approved => "approved",
        ParticipationState.Disputed => "disputed",
        ParticipationState.Declined => "declined",
        ParticipationState.Withdrawn => "withdrawn",
        ParticipationState.Expired => "expired",
        _ => "unknown",
    };

    /// <summary>The name of a state of a slot.</summary>
    private static string SlotStateText(TaskSlotState state) => state switch
    {
        TaskSlotState.Available => "available",
        TaskSlotState.Leased => "leased",
        TaskSlotState.Active => "active",
        TaskSlotState.Submitted => "submitted",
        TaskSlotState.Disputed => "disputed",
        TaskSlotState.Settled => "settled",
        TaskSlotState.Refunded => "refunded",
        _ => "unknown",
    };

    private static ParticipationCommandResult NotFound() =>
        new(ParticipationCommandStatus.NotFound, Message: "The seat does not exist.");

    private static ParticipationCommandResult Forbidden(string message) =>
        new(ParticipationCommandStatus.Forbidden, Message: message);

    private static ParticipationCommandResult VersionConflict(int currentVersion) =>
        new(
            ParticipationCommandStatus.VersionConflict,
            CurrentVersion: currentVersion,
            Message: "The task was changed somewhere else.");

    private static ParticipationCommandResult InvalidTransition(string message) =>
        new(ParticipationCommandStatus.InvalidTransition, Message: message);

    private static ParticipationCommandResult StateConflict(string message) =>
        new(ParticipationCommandStatus.StateConflict, Message: message);

    private static ParticipationCommandResult Conflict() =>
        new(
            ParticipationCommandStatus.StateConflict,
            Message: "The seat was changed somewhere else.");

    /// <summary>The seat with the task, the project and the rights of the actor.</summary>
    private sealed record ParticipationAccess(
        Participation Participation,
        MarketplaceTask Task,
        ProjectAccessView ProjectAccess,
        bool CanManage,
        bool IsContributor);
}
