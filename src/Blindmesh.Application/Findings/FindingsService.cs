using System.Text.Json;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Credits;
using Blindmesh.Application.Moderation;
using Blindmesh.Application.Projects;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Findings;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Shared;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Findings;

/// <summary>
/// The evidence and the review of the marketplace. The service owns the
/// submissions, the findings and the private files of the evidence, and it
/// settles the accepted work through the credit module. It never writes a
/// ledger row itself: only the credit module does that.
/// </summary>
public sealed class FindingsService(
    IFindingsStore store,
    ITaskStore tasks,
    IParticipationStore participations,
    ParticipationService participationService,
    ProjectService projects,
    ICreditService credits,
    ModerationService moderation,
    IObjectStorage storage,
    IOutboxWriter outbox,
    IClock clock)
{
    /// <summary>The number of submissions one sweep of the deadlines touches.</summary>
    public const int SweepLimit = 50;

    /// <summary>
    /// The contributor sent the evidence of a seat. The first revision opens the
    /// submission, a correction adds a revision to the same submission and
    /// keeps the revisions that the owner already read.
    /// </summary>
    public async Task<FindingsCommandResult> SubmitAsync(
        FindingsActor actor,
        Guid participationId,
        SubmissionInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in contributor is required to send the evidence.");
        }

        var found = await participations.FindParticipationAsync(participationId, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var task = await tasks.LockTaskAsync(found.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), task.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        var participation = await participations.LockParticipationAsync(participationId, cancellationToken);

        if (participation is null)
        {
            return NotFound();
        }

        if (participation.ContributorId != actor.UserId)
        {
            return NotFound();
        }

        if (participation.State is not (ParticipationState.Running or ParticipationState.ChangesRequested))
        {
            return StateConflict("The work of this seat does not accept evidence.");
        }

        if (task.Status == TaskState.Cancelled)
        {
            return StateConflict("This task was cancelled and does not accept evidence.");
        }

        var steps = await tasks.ListStepsAsync([task.Id], cancellationToken);
        var taskSteps = steps.GetValueOrDefault(task.Id, []);

        var errors = Validate(input, task, taskSteps);

        if (errors.Count > 0)
        {
            return new FindingsCommandResult(FindingsCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var now = clock.UtcNow;
        var existing = await store.FindSubmissionByParticipationAsync(participationId, cancellationToken);
        Submission submission;
        var number = 1;

        var slot = participation.SlotId is Guid slotId
            ? await tasks.LockSlotAsync(slotId, cancellationToken)
            : null;

        if (existing is null)
        {
            submission = new Submission
            {
                Id = Guid.NewGuid(),
                ParticipationId = participationId,
                TaskId = task.Id,
                ProjectId = task.ProjectId,
                ContributorId = participation.ContributorId,
                OwnerId = task.OwnerId,
                State = SubmissionState.Submitted,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
            };

            await store.AddSubmissionAsync(submission, cancellationToken);
        }
        else
        {
            if (existing.State != SubmissionState.ChangesRequested)
            {
                return StateConflict("The evidence of this seat was already sent.");
            }

            submission = await store.LockSubmissionAsync(existing.Id, cancellationToken) ?? existing;
            number = submission.RevisionNumber + 1;
        }

        var revision = new SubmissionRevision
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Number = number,
            Summary = input.Summary.Trim(),
            ScopeCompleted = input.ScopeCompleted,
            StepResults = JsonSerializer.Serialize(input.Steps
                .Select(step => new { position = step.Position, title = step.Title.Trim(), result = step.Result.Trim() })
                .ToArray()),
            TestedAreas = JsonSerializer.Serialize(input.TestedAreas
                .Select(area => area.Trim())
                .ToArray()),
            CleanResultExplanation = input.CleanResultExplanation.Trim(),
            CreatedAt = now,
        };

        await store.AddRevisionAsync(revision, cancellationToken);

        foreach (var finding in input.Findings)
        {
            await store.AddFindingAsync(new Finding
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                RevisionId = revision.Id,
                ContributorId = participation.ContributorId,
                Category = ParseCategory(finding.Category),
                Severity = ParseSeverity(finding.Severity),
                Title = finding.Title.Trim(),
                Reproduction = finding.Reproduction.Trim(),
                ExpectedResult = finding.ExpectedResult.Trim(),
                ActualResult = finding.ActualResult.Trim(),
                State = FindingState.Submitted,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
            }, cancellationToken);
        }

        var previousState = submission.State;
        var previousParticipationState = participation.State;
        submission.State = SubmissionState.Submitted;
        submission.Summary = revision.Summary;
        submission.ScopeCompleted = revision.ScopeCompleted;
        submission.RevisionNumber = number;
        submission.SubmittedAt = now;
        submission.CorrectionDeadline = null;
        submission.UpdatedAt = now;
        submission.Version += 1;

        await store.AddStatusChangeAsync(new SubmissionStatusChange
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            FromStatus = StateText(previousState),
            ToStatus = StateText(SubmissionState.Submitted),
            ChangedBy = actor.UserId!.Value,
            ChangedAt = now,
        }, cancellationToken);

        participation.State = ParticipationState.Submitted;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await participations.AddStatusChangeAsync(new ParticipationStatusChange
        {
            Id = Guid.NewGuid(),
            ParticipationId = participation.Id,
            FromStatus = ParticipationService.ParticipationStateText(previousParticipationState),
            ToStatus = ParticipationService.ParticipationStateText(ParticipationState.Submitted),
            ChangedBy = actor.UserId!.Value,
            ChangedAt = now,
        }, cancellationToken);

        if (slot is not null)
        {
            slot.State = TaskSlotState.Submitted;
        }

        await store.SaveChangesAsync(cancellationToken);
        await participationService.RecomputeTaskStatusAsync(task, cancellationToken);

        var detail = await BuildDetailAsync(submission, task, actor, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Created, Submission: detail);
    }

    /// <summary>
    /// The detail of a submission with its revisions, its findings and its
    /// history. Only the contributor of the seat and the owner of the task may
    /// read it; every other actor gets the answer of a resource that is not
    /// there.
    /// </summary>
    public async Task<FindingsCommandResult> GetAsync(
        FindingsActor actor,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await store.FindSubmissionAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        var task = await tasks.FindTaskAsync(submission.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), submission.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (!MayRead(actor, submission, task, access))
        {
            return NotFound();
        }

        var detail = await BuildDetailAsync(submission, task, actor, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Submission: detail);
    }

    /// <summary>
    /// One finding of a submission with its history. Only the contributor of
    /// the seat and the owner of the task may read it.
    /// </summary>
    public async Task<FindingsCommandResult> GetFindingAsync(
        FindingsActor actor,
        Guid findingId,
        CancellationToken cancellationToken)
    {
        var finding = await store.FindFindingAsync(findingId, cancellationToken);

        if (finding is null)
        {
            return NotFound();
        }

        var submission = await store.FindSubmissionAsync(finding.SubmissionId, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        var task = await tasks.FindTaskAsync(submission.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), submission.ProjectId, cancellationToken);

        if (access is null || !MayRead(actor, submission, task, access))
        {
            return NotFound();
        }

        var view = await BuildFindingViewAsync(finding, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Finding: view);
    }

    /// <summary>
    /// The evidence of the tasks of a project that the owner may review.
    /// </summary>
    public async Task<FindingsCommandResult> ListReviewsAsync(
        FindingsActor actor,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await projects.GetAccessAsync(ToProjectActor(actor), projectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        var rows = await store.ListReviewsAsync(projectId, cancellationToken);
        var readable = rows
            .Where(row => actor.IsAuthenticated &&
                (row.TaskOwnerId == actor.UserId || access.IsOwner))
            .ToArray();

        if (readable.Length == 0)
        {
            return Forbidden("The reviews of this project are not readable.");
        }

        var usernames = await tasks.ListUsernamesAsync(
            readable.Select(row => row.ContributorId).Distinct().ToArray(),
            cancellationToken);

        var reviews = readable
            .Select(row => new SubmissionSummaryView(
                row.SubmissionId,
                row.TaskId,
                row.TaskTitle,
                usernames.GetValueOrDefault(row.ContributorId, string.Empty),
                StateText(row.State),
                row.Summary,
                row.ScopeCompleted,
                row.SubmittedAt,
                row.CorrectionDeadline,
                row.BaseReward,
                row.SlotBonus,
                row.BonusGranted,
                row.Version))
            .ToArray();

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Reviews: reviews);
    }

    /// <summary>
    /// The owner accepts the work of a seat. The pending credits of the seat and
    /// the granted bonus become the earned credits of the contributor, and the
    /// unused bonus of the slot goes back to the publisher. The settlement and
    /// the state of the submission are written in the same transaction.
    /// </summary>
    public async Task<FindingsCommandResult> AcceptAsync(
        FindingsActor actor,
        Guid submissionId,
        AcceptSubmissionInput input,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in owner is required to accept the work.");
        }

        var found = await store.FindSubmissionAsync(submissionId, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var task = await tasks.LockTaskAsync(found.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), found.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (!MayReview(actor, found, task, access))
        {
            return NotFound();
        }

        var participation = await participations.LockParticipationAsync(found.ParticipationId, cancellationToken);

        if (participation is null)
        {
            return NotFound();
        }

        var slot = participation.SlotId is Guid slotId
            ? await tasks.LockSlotAsync(slotId, cancellationToken)
            : null;

        var submission = await store.LockSubmissionAsync(found.Id, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        if (submission.State != SubmissionState.Submitted)
        {
            return StateConflict("The evidence of this seat does not wait for an acceptance.");
        }

        if (input.ExpectedVersion is int expected && expected != submission.Version)
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.VersionConflict,
                CurrentVersion: submission.Version,
                Message: "The evidence was changed somewhere else.");
        }

        if (input.Bonus < 0)
        {
            return ValidationFailed("bonus", "The bonus of an acceptance may not be negative.");
        }

        if (input.Bonus > task.SlotBonus)
        {
            return ValidationFailed(
                "bonus",
                $"The bonus of an acceptance may not be more than {task.SlotBonus} credits.");
        }

        return await AcceptCoreAsync(
            actor,
            submission,
            participation,
            task,
            slot,
            input.Bonus,
            autoAccepted: false,
            idempotencyKey,
            cancellationToken);
    }

    /// <summary>
    /// The owner asks for the one correction of the seat. The contributor has
    /// forty eight hours to answer it. A second request is refused.
    /// </summary>
    public async Task<FindingsCommandResult> RequestChangesAsync(
        FindingsActor actor,
        Guid submissionId,
        RequestChangesInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in owner is required to ask for a correction.");
        }

        var note = input.Note?.Trim() ?? string.Empty;

        if (note.Length is < 1 or > SubmissionRules.DecisionNoteMaxLength)
        {
            return ValidationFailed(
                "note",
                $"The note of a correction holds 1 to {SubmissionRules.DecisionNoteMaxLength} characters.");
        }

        var found = await store.FindSubmissionAsync(submissionId, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var task = await tasks.LockTaskAsync(found.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), found.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (!MayReview(actor, found, task, access))
        {
            return NotFound();
        }

        var participation = await participations.LockParticipationAsync(found.ParticipationId, cancellationToken);

        if (participation is null)
        {
            return NotFound();
        }

        var submission = await store.LockSubmissionAsync(found.Id, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        if (submission.State != SubmissionState.Submitted)
        {
            return StateConflict("The evidence of this seat does not wait for a correction.");
        }

        if (submission.CorrectionRequestedAt is not null)
        {
            return StateConflict("The owner already asked for the one correction of this seat.");
        }

        if (input.ExpectedVersion is int expected && expected != submission.Version)
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.VersionConflict,
                CurrentVersion: submission.Version,
                Message: "The evidence was changed somewhere else.");
        }

        var now = clock.UtcNow;
        var previousState = submission.State;
        var previousParticipationState = participation.State;

        submission.State = SubmissionState.ChangesRequested;
        submission.CorrectionRequestedAt = now;
        submission.CorrectionDeadline = SubmissionRules.CorrectionDeadline(now);
        submission.UpdatedAt = now;
        submission.Version += 1;

        await store.AddStatusChangeAsync(new SubmissionStatusChange
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            FromStatus = StateText(previousState),
            ToStatus = StateText(SubmissionState.ChangesRequested),
            ChangedBy = actor.UserId!.Value,
            ChangedAt = now,
        }, cancellationToken);

        participation.State = ParticipationState.ChangesRequested;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await participations.AddStatusChangeAsync(new ParticipationStatusChange
        {
            Id = Guid.NewGuid(),
            ParticipationId = participation.Id,
            FromStatus = ParticipationService.ParticipationStateText(previousParticipationState),
            ToStatus = ParticipationService.ParticipationStateText(ParticipationState.ChangesRequested),
            ChangedBy = actor.UserId!.Value,
            ChangedAt = now,
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);
        await participationService.RecomputeTaskStatusAsync(task, cancellationToken);

        var detail = await BuildDetailAsync(submission, task, actor, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Submission: detail);
    }

    /// <summary>
    /// The owner opens a dispute about the completion of the seat. The funds
    /// wait for the arbitration of another package: this package marks the
    /// submission as disputed and moves no credit.
    /// </summary>
    public async Task<FindingsCommandResult> DisputeAsync(
        FindingsActor actor,
        Guid submissionId,
        DisputeSubmissionInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in owner is required to open a dispute.");
        }

        var reason = input.Reason?.Trim() ?? string.Empty;

        if (reason.Length is < 1 or > SubmissionRules.DisputeReasonMaxLength)
        {
            return ValidationFailed(
                "reason",
                $"The reason of a dispute holds 1 to {SubmissionRules.DisputeReasonMaxLength} characters.");
        }

        // The dispute is opened by the moderation module. Both routes of the
        // product reach the same state machine, and only one case may be open
        // for a submission.
        var opened = await moderation.OpenDisputeAsync(
            new ModerationActor(actor.UserId, actor.Username, UserRole.User),
            submissionId,
            new OpenDisputeInput(reason, input.ExpectedVersion),
            cancellationToken);

        if (opened.Status == ModerationCommandStatus.NotFound
            || opened.Status == ModerationCommandStatus.Forbidden)
        {
            return NotFound();
        }

        if (opened.Status == ModerationCommandStatus.ValidationFailed)
        {
            return ValidationFailed(
                "reason",
                opened.Message ?? $"The reason of a dispute holds 1 to {SubmissionRules.DisputeReasonMaxLength} characters.");
        }

        if (opened.Status == ModerationCommandStatus.VersionConflict)
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.VersionConflict,
                CurrentVersion: opened.CurrentVersion,
                Message: "The evidence was changed somewhere else.");
        }

        if (opened.Status == ModerationCommandStatus.StateConflict)
        {
            return StateConflict(opened.Message ?? "The evidence of this seat does not wait for a dispute.");
        }

        if (opened.Status is not (ModerationCommandStatus.Created or ModerationCommandStatus.Succeeded))
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.Unavailable,
                Message: opened.Message ?? "The dispute could not be opened.");
        }

        var submission = await store.FindSubmissionAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        var task = await tasks.FindTaskAsync(submission.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var detail = await BuildDetailAsync(submission, task, actor, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Submission: detail);
    }

    /// <summary>
    /// The owner classifies a finding. The decision never decides the earned
    /// base pay of the submission: a rejected finding confiscates nothing.
    /// </summary>
    public async Task<FindingsCommandResult> PatchFindingAsync(
        FindingsActor actor,
        Guid findingId,
        PatchFindingInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in owner is required to classify a finding.");
        }

        var found = await store.FindFindingAsync(findingId, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var submissionFound = await store.FindSubmissionAsync(found.SubmissionId, cancellationToken);

        if (submissionFound is null)
        {
            return NotFound();
        }

        var task = await tasks.LockTaskAsync(submissionFound.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), submissionFound.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (!MayReview(actor, submissionFound, task, access))
        {
            return NotFound();
        }

        if (!TryParseFindingState(input.State, out var target))
        {
            return ValidationFailed("state", "The state of a finding is not one of the known states.");
        }

        if (input.Note is { } note && note.Trim().Length > SubmissionRules.DecisionNoteMaxLength)
        {
            return ValidationFailed(
                "note",
                $"The note of a decision holds at most {SubmissionRules.DecisionNoteMaxLength} characters.");
        }

        var finding = await store.LockFindingAsync(findingId, cancellationToken);

        if (finding is null)
        {
            return NotFound();
        }

        if (input.ExpectedVersion is int expected && expected != finding.Version)
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.VersionConflict,
                CurrentVersion: finding.Version,
                Message: "The finding was changed somewhere else.");
        }

        if (finding.State == target)
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.InvalidTransition,
                Message: "The finding already carries this state.");
        }

        if (!FindingRules.Allows(finding.State, target))
        {
            return new FindingsCommandResult(
                FindingsCommandStatus.InvalidTransition,
                Message: $"A finding may not move from {FindingStateText(finding.State)} to {FindingStateText(target)}.");
        }

        var now = clock.UtcNow;
        var previousState = finding.State;

        finding.State = target;
        finding.DecisionNote = input.Note?.Trim();
        finding.DecidedBy = actor.UserId;
        finding.DecidedAt = now;
        finding.UpdatedAt = now;
        finding.Version += 1;

        await store.AddFindingHistoryAsync(new FindingHistoryEntry
        {
            Id = Guid.NewGuid(),
            FindingId = finding.Id,
            FromStatus = FindingStateText(previousState),
            ToStatus = FindingStateText(target),
            ChangedBy = actor.UserId!.Value,
            ChangedAt = now,
        }, cancellationToken);

        if (target == FindingState.Confirmed)
        {
            // The finding is confirmed. The event is written inside the
            // transaction of the command, so the decision and its event are
            // stored together or not at all.
            await outbox.EnqueueAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = FindingsEventTypes.FindingConfirmed,
                Version = 1,
                ActorId = actor.UserId,
                AggregateId = finding.Id,
                OccurredAt = now,
                Payload = JsonSerializer.Serialize(new FindingsEventPayload(
                    Guid.Empty,
                    submissionFound.Id,
                    submissionFound.TaskId,
                    submissionFound.ProjectId,
                    finding.ContributorId,
                    task.OwnerId,
                    finding.Id,
                    0,
                    "confirmed_finding",
                    now)),
            }, cancellationToken);
        }

        await store.SaveChangesAsync(cancellationToken);

        var view = await BuildFindingViewAsync(finding, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Finding: view);
    }

    /// <summary>
    /// The contributor declares a private file of the evidence. The file is
    /// quarantined from this moment: it is never readable while it waits for
    /// its bytes and for its check.
    /// </summary>
    public async Task<FindingsCommandResult> CreateAttachmentIntentAsync(
        FindingsActor actor,
        AttachmentIntentInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return Forbidden("A signed in contributor is required to attach a file.");
        }

        var found = await store.FindSubmissionAsync(input.SubmissionId, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var task = await tasks.LockTaskAsync(found.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), found.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (found.ContributorId != actor.UserId)
        {
            return NotFound();
        }

        if (found.State is not (SubmissionState.Submitted or SubmissionState.ChangesRequested))
        {
            return StateConflict("The evidence of this seat does not accept files.");
        }

        var revisions = await store.ListRevisionsAsync(found.Id, cancellationToken);
        var revision = revisions.LastOrDefault();

        if (revision is null)
        {
            return StateConflict("The evidence of this seat has no revision yet.");
        }

        var count = await store.CountAttachmentsAsync(revision.Id, cancellationToken);

        if (count >= AttachmentRules.MaxAttachmentsPerRevision)
        {
            return ValidationFailed(
                "file_name",
                $"A revision carries at most {AttachmentRules.MaxAttachmentsPerRevision} files.");
        }

        var fileName = input.FileName?.Trim() ?? string.Empty;

        if (fileName.Length is < 1 or > 200)
        {
            return ValidationFailed("file_name", "The name of a file holds 1 to 200 characters.");
        }

        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            return ValidationFailed("file_name", "The name of a file carries no path.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!AttachmentRules.IsAllowedExtension(extension))
        {
            return ValidationFailed(
                "file_name",
                "Only a text, a log and a picture of a supported format may be attached.");
        }

        var contentType = input.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!AttachmentRules.IsAllowedContentType(contentType))
        {
            return ValidationFailed(
                "content_type",
                "Only a text, a log and a picture of a supported format may be attached.");
        }

        var expected = AttachmentRules.ContentTypeFor(extension);

        if (expected is not null && !string.Equals(expected, contentType, StringComparison.Ordinal))
        {
            return ValidationFailed(
                "content_type",
                "The declared content type does not match the name of the file.");
        }

        var limit = AttachmentRules.MaxBytesFor(contentType);

        if (limit is null)
        {
            return ValidationFailed("content_type", "This kind of file may not be attached.");
        }

        if (input.SizeBytes <= 0 || input.SizeBytes > limit)
        {
            return ValidationFailed(
                "size_bytes",
                $"This kind of file holds at most {limit} bytes.");
        }

        var now = clock.UtcNow;
        var attachmentId = Guid.NewGuid();

        var attachment = new Attachment
        {
            Id = attachmentId,
            SubmissionId = found.Id,
            RevisionId = revision.Id,
            ContributorId = found.ContributorId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = input.SizeBytes,
            StorageKey = $"evidence/{found.Id:D}/{revision.Id:D}/{attachmentId:D}",
            State = AttachmentState.Quarantined,
            CreatedAt = now,
        };

        await store.AddAttachmentAsync(attachment, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Created, Attachment: ToAttachmentView(attachment));
    }

    /// <summary>
    /// The check of the bytes of a quarantined file and the write of the bytes
    /// into the private object storage. The check and the write are network
    /// work, so they never run inside the transaction of the command.
    /// </summary>
    public async Task<AttachmentPreparation> PrepareAttachmentAsync(
        FindingsActor actor,
        Guid attachmentId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var attachment = await store.FindAttachmentAsync(attachmentId, cancellationToken);

        if (attachment is null)
        {
            return AttachmentPreparation.Refused("The file is not part of the evidence.");
        }

        var submission = await store.FindSubmissionAsync(attachment.SubmissionId, cancellationToken);

        if (submission is null)
        {
            return AttachmentPreparation.Refused("The file is not part of the evidence.");
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), submission.ProjectId, cancellationToken);

        if (access is null || submission.ContributorId != actor.UserId)
        {
            return AttachmentPreparation.Refused("The file is not part of the evidence.");
        }

        if (attachment.State != AttachmentState.Quarantined)
        {
            return AttachmentPreparation.Refused("The file was already released or refused.");
        }

        if (content.Length != attachment.SizeBytes)
        {
            return AttachmentPreparation.Refused("The bytes of the file do not match the declaration.");
        }

        var limit = AttachmentRules.MaxBytesFor(attachment.ContentType);

        if (limit is null || content.Length > limit)
        {
            return AttachmentPreparation.Refused("The file is larger than the allowed size.");
        }

        var detected = AttachmentRules.DetectContentType(content.Span);

        if (detected is null)
        {
            return AttachmentPreparation.Refused("The bytes of the file do not match the allowed formats.");
        }

        if (!IsSameKind(detected, attachment.ContentType))
        {
            return AttachmentPreparation.Refused("The bytes of the file do not match the declared format.");
        }

        if (AttachmentRules.HoldsTestMarker(content.Span))
        {
            return AttachmentPreparation.Refused("The file holds the marker of the test file of the scanners.");
        }

        if (!storage.IsConfigured)
        {
            throw new ObjectStorageUnavailableException();
        }

        await storage.PutAsync(
            attachment.StorageKey,
            content,
            attachment.ContentType,
            cancellationToken);

        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content.Span)).ToLowerInvariant();

        return AttachmentPreparation.Accepted(digest);
    }

    /// <summary>
    /// The release of a file after its check. The row is written inside the
    /// transaction of the command and the authorization is checked again.
    /// </summary>
    public async Task<FindingsCommandResult> ReleaseAttachmentAsync(
        FindingsActor actor,
        Guid attachmentId,
        AttachmentPreparation preparation,
        CancellationToken cancellationToken)
    {
        var found = await store.FindAttachmentAsync(attachmentId, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var submission = await store.FindSubmissionAsync(found.SubmissionId, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), submission.ProjectId, cancellationToken);

        if (access is null || submission.ContributorId != actor.UserId)
        {
            return NotFound();
        }

        var attachment = await store.LockAttachmentAsync(attachmentId, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        if (attachment.State != AttachmentState.Quarantined)
        {
            return StateConflict("The file was already released or refused.");
        }

        var now = clock.UtcNow;

        if (preparation.Passed)
        {
            attachment.State = AttachmentState.Scanned;
            attachment.Sha256 = preparation.Sha256;
            attachment.ScannedAt = now;
            attachment.RejectionReason = null;
        }
        else
        {
            attachment.State = AttachmentState.Rejected;
            attachment.RejectionReason = preparation.Reason;
        }

        await store.SaveChangesAsync(cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Attachment: ToAttachmentView(attachment));
    }

    /// <summary>
    /// The signed address of a private file. The authorization is checked again
    /// for every address, so a reader that lost the membership never renews an
    /// address it held before.
    /// </summary>
    public async Task<FindingsCommandResult> CreateDownloadAsync(
        FindingsActor actor,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await store.FindAttachmentAsync(attachmentId, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        var submission = await store.FindSubmissionAsync(attachment.SubmissionId, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        var task = await tasks.FindTaskAsync(submission.TaskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var access = await projects.GetAccessAsync(ToProjectActor(actor), submission.ProjectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (!MayRead(actor, submission, task, access))
        {
            return NotFound();
        }

        if (attachment.State != AttachmentState.Scanned)
        {
            return StateConflict("The file is not released yet.");
        }

        if (!storage.IsConfigured)
        {
            throw new ObjectStorageUnavailableException();
        }

        var lifetime = TimeSpan.FromSeconds(SubmissionRules.DownloadUrlSeconds);
        var url = await storage.CreateSignedUrlAsync(
            attachment.StorageKey,
            attachment.FileName,
            attachment.ContentType,
            lifetime,
            cancellationToken);

        var download = new AttachmentDownloadView(
            url,
            clock.UtcNow.Add(lifetime),
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Download: download);
    }

    /// <summary>
    /// The sweep of the deadlines of the review. It accepts the base of a seat
    /// that waited longer than the review timeout and it opens a dispute for a
    /// correction that was not answered in time. The sweep never resolves a
    /// dispute: the arbitration is another package.
    /// </summary>
    public async Task<ReviewSweepReport> SweepOverdueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var overdue = await store.ListOverdueAsync(now, SweepLimit, cancellationToken);
        var accepted = 0;
        var disputed = 0;

        foreach (var candidate in overdue)
        {
            var task = await tasks.LockTaskAsync(candidate.TaskId, cancellationToken);

            if (task is null)
            {
                continue;
            }

            var participation = await participations.LockParticipationAsync(candidate.ParticipationId, cancellationToken);

            if (participation is null)
            {
                continue;
            }

            var slot = participation.SlotId is Guid slotId
                ? await tasks.LockSlotAsync(slotId, cancellationToken)
                : null;

            var submission = await store.LockSubmissionAsync(candidate.Id, cancellationToken);

            if (submission is null)
            {
                continue;
            }

            if (submission.State == SubmissionState.Submitted &&
                submission.CorrectionRequestedAt is null &&
                submission.SubmittedAt is DateTimeOffset submittedAt &&
                now >= SubmissionRules.ReviewDeadline(submittedAt))
            {
                var result = await AcceptCoreAsync(
                    SystemActor(),
                    submission,
                    participation,
                    task,
                    slot,
                    0,
                    autoAccepted: true,
                    idempotencyKey: null,
                    cancellationToken);

                if (result.Status == FindingsCommandStatus.Succeeded)
                {
                    accepted += 1;
                }
            }
        }

        var corrections = await store.ListOverdueCorrectionsAsync(now, SweepLimit, cancellationToken);

        foreach (var candidate in corrections)
        {
            var task = await tasks.LockTaskAsync(candidate.TaskId, cancellationToken);

            if (task is null)
            {
                continue;
            }

            var participation = await participations.LockParticipationAsync(candidate.ParticipationId, cancellationToken);

            if (participation is null)
            {
                continue;
            }

            var slot = participation.SlotId is Guid slotId
                ? await tasks.LockSlotAsync(slotId, cancellationToken)
                : null;

            var submission = await store.LockSubmissionAsync(candidate.Id, cancellationToken);

            if (submission is null || submission.State != SubmissionState.ChangesRequested)
            {
                continue;
            }

            var result = await moderation.OpenDisputeAsync(
                new ModerationActor(Guid.Empty, "system", UserRole.Moderator),
                candidate.Id,
                new OpenDisputeInput("The correction of the contributor was not sent in time."),
                cancellationToken);

            if (result.Status is ModerationCommandStatus.Created or ModerationCommandStatus.Succeeded)
            {
                disputed += 1;
            }
        }

        return new ReviewSweepReport(accepted, disputed);
    }

    /// <summary>
    /// The acceptance of the work of a seat with its settlement. The pending
    /// credits of the seat and the granted bonus become the earned credits of
    /// the contributor, and the unused bonus of the slot goes back to the
    /// publisher. Only the credit module writes a ledger row.
    /// </summary>
    private async Task<FindingsCommandResult> AcceptCoreAsync(
        FindingsActor actor,
        Submission submission,
        Participation participation,
        MarketplaceTask task,
        TaskSlot? slot,
        long bonus,
        bool autoAccepted,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var reference = TaskService.EscrowReference(task.Id);
        var usernames = await tasks.ListUsernamesAsync(
            [task.OwnerId, participation.ContributorId],
            cancellationToken);

        var publisher = new CreditActor(
            task.OwnerId,
            usernames.GetValueOrDefault(task.OwnerId, string.Empty));
        var contributor = new CreditActor(
            participation.ContributorId,
            usernames.GetValueOrDefault(participation.ContributorId, string.Empty));

        if (bonus > 0)
        {
            var pending = await credits.BeginPendingAsync(
                publisher,
                new CreditPendingCommand(reference, participation.ContributorId, bonus, idempotencyKey),
                cancellationToken);

            if (pending.Status != CreditCommandStatus.Succeeded)
            {
                return StateConflict(pending.Message ?? "The bonus of the seat could not be moved.");
            }
        }

        var settled = await credits.SettleAsync(
            contributor,
            new CreditSettleCommand(reference, task.BaseReward + bonus, idempotencyKey),
            cancellationToken);

        if (settled.Status != CreditCommandStatus.Succeeded)
        {
            return StateConflict(settled.Message ?? "The credits of the seat could not be settled.");
        }

        var unused = task.SlotBonus - bonus;

        if (unused > 0)
        {
            var refunded = await credits.RefundUnstartedAsync(
                publisher,
                new CreditRefundCommand(reference, unused, idempotencyKey),
                cancellationToken);

            if (refunded.Status != CreditCommandStatus.Succeeded)
            {
                return StateConflict(refunded.Message ?? "The unused bonus of the slot could not be returned.");
            }
        }

        var previousState = submission.State;

        submission.State = SubmissionState.Accepted;
        submission.BonusGranted = bonus;
        submission.SettledCredits = task.BaseReward + bonus;
        submission.ReviewedAt = now;
        submission.CorrectionDeadline = null;
        submission.AutoAccepted = autoAccepted;
        submission.UpdatedAt = now;
        submission.Version += 1;

        await store.AddStatusChangeAsync(new SubmissionStatusChange
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            FromStatus = StateText(previousState),
            ToStatus = StateText(SubmissionState.Accepted),
            ChangedBy = actor.UserId ?? submission.OwnerId,
            ChangedAt = now,
        }, cancellationToken);

        participation.State = ParticipationState.Approved;
        participation.EndedAt = now;
        participation.UpdatedAt = now;
        participation.Version += 1;

        await participations.AddStatusChangeAsync(new ParticipationStatusChange
        {
            Id = Guid.NewGuid(),
            ParticipationId = participation.Id,
            FromStatus = ParticipationService.ParticipationStateText(ParticipationState.Submitted),
            ToStatus = ParticipationService.ParticipationStateText(ParticipationState.Approved),
            ChangedBy = actor.UserId ?? submission.OwnerId,
            ChangedAt = now,
        }, cancellationToken);

        if (slot is not null)
        {
            slot.State = TaskSlotState.Settled;
        }

        // The seat is finally accepted. The event is written inside the
        // transaction of the command, so the outcome and its event are stored
        // together or not at all.
        await outbox.EnqueueAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = FindingsEventTypes.ParticipationAccepted,
            Version = 1,
            ActorId = actor.UserId,
            AggregateId = participation.Id,
            OccurredAt = now,
            Payload = JsonSerializer.Serialize(new FindingsEventPayload(
                participation.Id,
                submission.Id,
                task.Id,
                submission.ProjectId,
                participation.ContributorId,
                task.OwnerId,
                null,
                task.BaseReward + bonus,
                "accepted_contribution",
                now)),
        }, cancellationToken);

        await store.SaveChangesAsync(cancellationToken);
        await participationService.RecomputeTaskStatusAsync(task, cancellationToken);

        var detail = await BuildDetailAsync(submission, task, actor, cancellationToken);

        return new FindingsCommandResult(FindingsCommandStatus.Succeeded, Submission: detail);
    }

    /// <summary>
    /// The committed state of a submission with its revisions, its findings and
    /// its history.
    /// </summary>
    private async Task<SubmissionDetailView> BuildDetailAsync(
        Submission submission,
        MarketplaceTask task,
        FindingsActor actor,
        CancellationToken cancellationToken)
    {
        var revisions = await store.ListRevisionsAsync(submission.Id, cancellationToken);
        var findings = await store.ListFindingsAsync(submission.Id, cancellationToken);
        var history = await store.ListSubmissionHistoryAsync(submission.Id, cancellationToken);
        var usernames = await tasks.ListUsernamesAsync(
            [submission.OwnerId, submission.ContributorId],
            cancellationToken);
        var projects = await tasks.ListProjectNamesAsync([submission.ProjectId], cancellationToken);

        var revisionViews = new List<SubmissionRevisionView>(revisions.Count);

        foreach (var revision in revisions)
        {
            var attachments = await store.ListAttachmentsAsync(revision.Id, cancellationToken);

            revisionViews.Add(new SubmissionRevisionView(
                revision.Id,
                revision.Number,
                revision.Summary,
                revision.ScopeCompleted,
                ReadStepResults(revision.StepResults),
                ReadTestedAreas(revision.TestedAreas),
                revision.CleanResultExplanation,
                revision.CreatedAt,
                attachments.Select(ToAttachmentView).ToArray()));
        }

        var findingViews = new List<FindingView>(findings.Count);

        foreach (var finding in findings)
        {
            findingViews.Add(await BuildFindingViewAsync(finding, cancellationToken));
        }

        var historyViews = history
            .Select(change => new SubmissionStatusChangeView(
                change.FromStatus,
                change.ToStatus,
                usernames.GetValueOrDefault(change.ChangedBy, string.Empty),
                change.ChangedAt))
            .ToArray();

        var isContributor = submission.ContributorId == actor.UserId;
        var mayReview = MayReview(actor, submission, task, null);
        var maySubmit = isContributor &&
            submission.State is SubmissionState.Submitted or SubmissionState.ChangesRequested;

        return new SubmissionDetailView(
            submission.Id,
            submission.ParticipationId,
            submission.TaskId,
            task.Title,
            submission.ProjectId,
            projects.GetValueOrDefault(submission.ProjectId, string.Empty),
            usernames.GetValueOrDefault(submission.OwnerId, string.Empty),
            usernames.GetValueOrDefault(submission.ContributorId, string.Empty),
            StateText(submission.State),
            TaskService.ModeNames(task.Modes),
            submission.Summary,
            submission.ScopeCompleted,
            revisionViews,
            findingViews,
            historyViews,
            task.BaseReward,
            task.SlotBonus,
            submission.BonusGranted,
            submission.SettledCredits,
            submission.SubmittedAt,
            submission.ReviewedAt,
            submission.CorrectionDeadline,
            submission.SubmittedAt is DateTimeOffset sentAt
                ? SubmissionRules.ReviewDeadline(sentAt)
                : null,
            submission.DisputeReason,
            maySubmit,
            mayReview && submission.State == SubmissionState.Submitted,
            mayReview &&
                submission.State == SubmissionState.Submitted &&
                submission.CorrectionRequestedAt is null,
            mayReview &&
                submission.State is SubmissionState.Submitted or SubmissionState.ChangesRequested,
            maySubmit,
            submission.Version);
    }

    private async Task<FindingView> BuildFindingViewAsync(Finding finding, CancellationToken cancellationToken)
    {
        var history = await store.ListFindingHistoryAsync(finding.Id, cancellationToken);
        var usernames = await tasks.ListUsernamesAsync(
            [finding.ContributorId, finding.DecidedBy ?? Guid.Empty],
            cancellationToken);

        return new FindingView(
            finding.Id,
            finding.SubmissionId,
            CategoryText(finding.Category),
            SeverityText(finding.Severity),
            finding.Title,
            finding.Reproduction,
            finding.ExpectedResult,
            finding.ActualResult,
            FindingStateText(finding.State),
            finding.DecisionNote,
            finding.DecidedBy is Guid decidedBy
                ? usernames.GetValueOrDefault(decidedBy, string.Empty)
                : string.Empty,
            finding.DecidedAt,
            finding.CreatedAt,
            finding.Version,
            history
                .Select(entry => new FindingHistoryView(
                    entry.FromStatus,
                    entry.ToStatus,
                    usernames.GetValueOrDefault(entry.ChangedBy, string.Empty),
                    entry.ChangedAt))
                .ToArray());
    }

    private static bool MayRead(
        FindingsActor actor,
        Submission submission,
        MarketplaceTask task,
        ProjectAccessView? access)
    {
        if (!actor.IsAuthenticated)
        {
            return false;
        }

        if (submission.ContributorId == actor.UserId)
        {
            return true;
        }

        return MayReview(actor, submission, task, access);
    }

    private static bool MayReview(
        FindingsActor actor,
        Submission submission,
        MarketplaceTask task,
        ProjectAccessView? access)
    {
        if (!actor.IsAuthenticated)
        {
            return false;
        }

        if (task.OwnerId == actor.UserId)
        {
            return true;
        }

        return access is not null && access.IsOwner;
    }

    private static FindingsCommandResult NotFound() =>
        new(FindingsCommandStatus.NotFound, Message: "The resource is not there.");

    private static FindingsCommandResult Forbidden(string message) =>
        new(FindingsCommandStatus.Forbidden, Message: message);

    private static FindingsCommandResult StateConflict(string message) =>
        new(FindingsCommandStatus.StateConflict, Message: message);

    private static FindingsCommandResult ValidationFailed(string field, string message) =>
        new(
            FindingsCommandStatus.ValidationFailed,
            FieldErrors: new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] },
            Message: message);

    private static FindingsActor SystemActor() => new(Guid.Empty, "system");

    private static ProjectActor ToProjectActor(FindingsActor actor) =>
        new(actor.UserId, actor.Username);

    private static bool IsSameKind(string detected, string declared) =>
        declared switch
        {
            "text/plain" or "text/markdown" or "text/csv" or "application/json" => detected == "text/plain",
            "image/png" => detected == "image/png",
            "image/jpeg" => detected == "image/jpeg",
            "image/webp" => detected == "image/webp",
            _ => false,
        };

    private static AttachmentView ToAttachmentView(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            AttachmentStateText(attachment.State),
            attachment.CreatedAt);

    private static IReadOnlyList<SubmissionStepResultView> ReadStepResults(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var results = new List<SubmissionStepResultView>();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                results.Add(new SubmissionStepResultView(
                    element.TryGetProperty("position", out var position) ? position.GetInt32() : 0,
                    element.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                    element.TryGetProperty("result", out var result) ? result.GetString() ?? string.Empty : string.Empty));
            }

            return results;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ReadTestedAreas(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.EnumerateArray()
                .Select(area => area.GetString() ?? string.Empty)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The wire name of the state of a submission.</summary>
    public static string StateText(SubmissionState state) => state switch
    {
        SubmissionState.Draft => "draft",
        SubmissionState.Submitted => "submitted",
        SubmissionState.ChangesRequested => "changes_requested",
        SubmissionState.Accepted => "accepted",
        SubmissionState.Disputed => "disputed",
        _ => "draft",
    };

    /// <summary>The wire name of the state of a finding.</summary>
    public static string FindingStateText(FindingState state) => state switch
    {
        FindingState.Submitted => "submitted",
        FindingState.Triaged => "triaged",
        FindingState.Confirmed => "confirmed",
        FindingState.Rejected => "rejected",
        FindingState.Duplicate => "duplicate",
        FindingState.Fixing => "fixing",
        FindingState.Resolved => "resolved",
        _ => "submitted",
    };

    /// <summary>The wire name of the category of a finding.</summary>
    public static string CategoryText(FindingCategory category) => category switch
    {
        FindingCategory.Bug => "bug",
        FindingCategory.Ux => "ux",
        FindingCategory.Performance => "performance",
        FindingCategory.Security => "security",
        FindingCategory.Accessibility => "accessibility",
        FindingCategory.Compatibility => "compatibility",
        FindingCategory.Documentation => "documentation",
        _ => "bug",
    };

    /// <summary>The wire name of the severity of a finding.</summary>
    public static string SeverityText(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Info => "info",
        FindingSeverity.Low => "low",
        FindingSeverity.Medium => "medium",
        FindingSeverity.High => "high",
        FindingSeverity.Critical => "critical",
        _ => "info",
    };

    /// <summary>The wire name of the state of a private file.</summary>
    public static string AttachmentStateText(AttachmentState state) => state switch
    {
        AttachmentState.Quarantined => "quarantined",
        AttachmentState.Scanned => "scanned",
        AttachmentState.Rejected => "rejected",
        _ => "quarantined",
    };

    private static FindingCategory ParseCategory(string value) => value.Trim().ToLowerInvariant() switch
    {
        "ux" => FindingCategory.Ux,
        "performance" => FindingCategory.Performance,
        "security" => FindingCategory.Security,
        "accessibility" => FindingCategory.Accessibility,
        "compatibility" => FindingCategory.Compatibility,
        "documentation" => FindingCategory.Documentation,
        _ => FindingCategory.Bug,
    };

    private static FindingSeverity ParseSeverity(string value) => value.Trim().ToLowerInvariant() switch
    {
        "low" => FindingSeverity.Low,
        "medium" => FindingSeverity.Medium,
        "high" => FindingSeverity.High,
        "critical" => FindingSeverity.Critical,
        _ => FindingSeverity.Info,
    };

    private static bool TryParseFindingState(string value, out FindingState state)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "triaged":
                state = FindingState.Triaged;
                return true;
            case "confirmed":
                state = FindingState.Confirmed;
                return true;
            case "rejected":
                state = FindingState.Rejected;
                return true;
            case "duplicate":
                state = FindingState.Duplicate;
                return true;
            case "fixing":
                state = FindingState.Fixing;
                return true;
            case "resolved":
                state = FindingState.Resolved;
                return true;
            case "submitted":
                state = FindingState.Submitted;
                return true;
            default:
                state = FindingState.Submitted;
                return false;
        }
    }

    /// <summary>
    /// The limits of the evidence. The result of a verification needs a result
    /// for every step of the task, an exploration needs its tested areas, and a
    /// clean result needs the explanation of the contributor.
    /// </summary>
    private static Dictionary<string, string[]> Validate(
        SubmissionInput input,
        MarketplaceTask task,
        IReadOnlyList<TaskStep> taskSteps)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var summary = input.Summary?.Trim() ?? string.Empty;

        if (summary.Length < SubmissionRules.SummaryMinLength || summary.Length > SubmissionRules.SummaryMaxLength)
        {
            errors["summary"] =
                [$"The summary holds {SubmissionRules.SummaryMinLength} to {SubmissionRules.SummaryMaxLength} characters."];
        }

        var steps = input.Steps ?? [];
        var areas = input.TestedAreas ?? [];
        var findings = input.Findings ?? [];

        if (task.Modes.HasFlag(TaskMode.Verify))
        {
            var missing = new List<string>();

            foreach (var step in taskSteps)
            {
                var result = steps.FirstOrDefault(candidate => candidate.Position == step.Position);

                if (result is null || string.IsNullOrWhiteSpace(result.Result))
                {
                    missing.Add(step.Title);
                }
            }

            var unknown = steps
                .Where(candidate => taskSteps.All(step => step.Position != candidate.Position))
                .Select(candidate => candidate.Position.ToString())
                .ToArray();

            if (missing.Count > 0 || unknown.Length > 0)
            {
                var messages = new List<string>();

                if (missing.Count > 0)
                {
                    messages.Add($"The steps {string.Join(", ", missing)} need a result.");
                }

                if (unknown.Length > 0)
                {
                    messages.Add($"The steps {string.Join(", ", unknown)} are not steps of this task.");
                }

                errors["steps"] = [.. messages];
            }
        }

        if (task.Modes.HasFlag(TaskMode.Explore))
        {
            if (areas.Count == 0)
            {
                errors["tested_areas"] = ["An exploration names the areas it covered."];
            }
            else if (areas.Count > SubmissionRules.MaxTestedAreas)
            {
                errors["tested_areas"] = [$"An exploration names at most {SubmissionRules.MaxTestedAreas} areas."];
            }
        }

        var explanation = input.CleanResultExplanation?.Trim() ?? string.Empty;

        if (explanation.Length > SubmissionRules.CleanResultMaxLength)
        {
            errors["clean_result_explanation"] =
                [$"The explanation of a clean result holds at most {SubmissionRules.CleanResultMaxLength} characters."];
        }

        if (findings.Count == 0)
        {
            if (task.Modes.HasFlag(TaskMode.Explore))
            {
                if (explanation.Length == 0)
                {
                    errors["clean_result_explanation"] = ["A clean result needs the explanation of the contributor."];
                }

                if (!task.AcceptsCleanFindings)
                {
                    errors["findings"] = ["This task does not accept a result without a finding."];
                }
            }
        }

        if (findings.Count > SubmissionRules.MaxFindingsPerRevision)
        {
            errors["findings"] = [$"A revision carries at most {SubmissionRules.MaxFindingsPerRevision} findings."];
        }

        for (var index = 0; index < findings.Count; index++)
        {
            var finding = findings[index];
            var prefix = $"findings[{index}]";
            var title = finding.Title?.Trim() ?? string.Empty;
            var reproduction = finding.Reproduction?.Trim() ?? string.Empty;
            var expected = finding.ExpectedResult?.Trim() ?? string.Empty;
            var actual = finding.ActualResult?.Trim() ?? string.Empty;

            if (!IsKnownCategory(finding.Category))
            {
                errors[$"{prefix}.category"] = ["The category of a finding is not one of the known categories."];
            }

            if (!IsKnownSeverity(finding.Severity))
            {
                errors[$"{prefix}.severity"] = ["The severity of a finding is not one of the known severities."];
            }

            if (title.Length is < 1 or > SubmissionRules.FindingTitleMaxLength)
            {
                errors[$"{prefix}.title"] =
                    [$"The title of a finding holds 1 to {SubmissionRules.FindingTitleMaxLength} characters."];
            }

            if (reproduction.Length > SubmissionRules.FindingReproductionMaxLength)
            {
                errors[$"{prefix}.reproduction"] =
                    [$"The reproduction of a finding holds at most {SubmissionRules.FindingReproductionMaxLength} characters."];
            }

            if (expected.Length > SubmissionRules.FindingResultMaxLength)
            {
                errors[$"{prefix}.expected_result"] =
                    [$"The expected result of a finding holds at most {SubmissionRules.FindingResultMaxLength} characters."];
            }

            if (actual.Length > SubmissionRules.FindingResultMaxLength)
            {
                errors[$"{prefix}.actual_result"] =
                    [$"The actual result of a finding holds at most {SubmissionRules.FindingResultMaxLength} characters."];
            }
        }

        return errors;
    }

    private static bool IsKnownCategory(string value) => value?.Trim().ToLowerInvariant() is
        "bug" or "ux" or "performance" or "security" or "accessibility" or "compatibility" or "documentation";

    private static bool IsKnownSeverity(string value) => value?.Trim().ToLowerInvariant() is
        "info" or "low" or "medium" or "high" or "critical";
}
