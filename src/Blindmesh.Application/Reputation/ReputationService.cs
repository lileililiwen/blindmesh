using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Identity;
using Blindmesh.Application.Projects;
using Blindmesh.Domain.Reputation;

namespace Blindmesh.Application.Reputation;

/// <summary>
/// The reputation of a contributor. The score is the sum of the deltas of the
/// stored events of the contributor, so it only comes from finally accepted
/// outcomes and it can never be bought. The module writes no ledger row; the
/// credits of a contributor are not its business.
/// </summary>
public sealed class ReputationService(
    IReputationStore store,
    IIdentityStore identity,
    ProjectService projects,
    IClock clock)
{
    /// <summary>
    /// Applies the acceptance of a seat. The contribution counts once, even
    /// when the same message arrives twice.
    /// </summary>
    public async Task<ReputationApplyResult> ApplyAcceptedAsync(
        ReputationEventInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return await ApplyAsync(input, ReputationCategory.AcceptedContribution, 1, cancellationToken);
    }

    /// <summary>
    /// Applies the confirmation of a finding. The confirmation counts once, even
    /// when the same message arrives twice.
    /// </summary>
    public async Task<ReputationApplyResult> ApplyFindingConfirmedAsync(
        ReputationEventInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return await ApplyAsync(input, ReputationCategory.ConfirmedFinding, 1, cancellationToken);
    }

    /// <summary>
    /// Applies the reversal of an accepted contribution. The line of the
    /// acceptance stays in the history; the reversal is a line of its own that
    /// takes the point back.
    /// </summary>
    public async Task<ReputationApplyResult> ApplyReversedAsync(
        ReputationEventInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return await ApplyAsync(input, ReputationCategory.ReversedContribution, -1, cancellationToken);
    }

    /// <summary>
    /// The public summary of a contributor. It holds the counts and never a
    /// source of a contribution. Null when the contributor does not exist.
    /// </summary>
    public async Task<ReputationSummaryView?> GetPublicAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var user = await identity.FindUserByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var score = await store.FindScoreAsync(user.Id, cancellationToken);

        return new ReputationSummaryView(
            user.Username,
            score?.CurrentScore ?? 0,
            score?.AcceptedContributions ?? 0,
            score?.ConfirmedFindings ?? 0,
            score?.ReversedContributions ?? 0,
            score?.UpdatedAt);
    }

    /// <summary>
    /// The public history of a contributor. The visibility of every source is
    /// read now, so a source that is not public any more is not part of the
    /// answer. Null when the contributor does not exist.
    /// </summary>
    public async Task<ReputationHistoryView?> GetPublicHistoryAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var user = await identity.FindUserByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var events = await store.ListEventsAsync(user.Id, cancellationToken);
        var views = new List<ReputationEventView>(events.Count);

        foreach (var reputationEvent in events)
        {
            var reference = await ReadReferenceAsync(
                ProjectActor.Anonymous,
                reputationEvent.SourceProjectId,
                cancellationToken);

            if (reference is null)
            {
                // The source of the line is not public, so the line is not part
                // of the public history.
                continue;
            }

            views.Add(new ReputationEventView(
                reputationEvent.Id,
                CategoryText(reputationEvent.Category),
                reputationEvent.Note,
                reputationEvent.Delta,
                reputationEvent.OccurredAt,
                reference.Name,
                null,
                null,
                null));
        }

        return await BuildHistoryAsync(user.Id, user.Username, views, cancellationToken);
    }

    /// <summary>
    /// The own history of a contributor. It holds every line of the
    /// contributor, with the references the contributor may read. Null when the
    /// account does not exist.
    /// </summary>
    public async Task<ReputationHistoryView?> GetOwnHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await identity.FindUserAsync(userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var events = await store.ListEventsAsync(userId, cancellationToken);
        var actor = new ProjectActor(userId, user.Username);
        var views = new List<ReputationEventView>(events.Count);

        foreach (var reputationEvent in events)
        {
            var reference = await ReadReferenceAsync(actor, reputationEvent.SourceProjectId, cancellationToken);

            views.Add(new ReputationEventView(
                reputationEvent.Id,
                CategoryText(reputationEvent.Category),
                reputationEvent.Note,
                reputationEvent.Delta,
                reputationEvent.OccurredAt,
                reference?.Name,
                reputationEvent.SourceTaskId,
                reputationEvent.SourceFindingId,
                reputationEvent.SourceParticipationId));
        }

        return await BuildHistoryAsync(userId, user.Username, views, cancellationToken);
    }

    /// <summary>The wire name of a category.</summary>
    public static string CategoryText(ReputationCategory category) => category switch
    {
        ReputationCategory.AcceptedContribution => "accepted_contribution",
        ReputationCategory.ConfirmedFinding => "confirmed_finding",
        ReputationCategory.ReversedContribution => "reversed_contribution",
        _ => "unknown",
    };

    /// <summary>
    /// The safe explanation of a category. The text never holds a title, a
    /// finding text, a party or a link.
    /// </summary>
    public static string LabelText(ReputationCategory category) => category switch
    {
        ReputationCategory.AcceptedContribution => "Accepted contribution",
        ReputationCategory.ConfirmedFinding => "Confirmed finding",
        ReputationCategory.ReversedContribution => "Reversed contribution",
        _ => "Reputation event",
    };

    private async Task<ReputationApplyResult> ApplyAsync(
        ReputationEventInput input,
        ReputationCategory category,
        int delta,
        CancellationToken cancellationToken)
    {
        var known = await store.FindEventBySourceAsync(input.SourceEventId, cancellationToken);

        if (known is not null)
        {
            // The same message arrived twice. Nothing changes and the counts
            // stay the ones that are already stored.
            return await ReadResultAsync(input.UserId, cancellationToken);
        }

        var source = category == ReputationCategory.ReversedContribution
            ? await FindAcceptedSourceAsync(input, cancellationToken)
            : null;

        var reputationEvent = new ReputationEvent
        {
            Id = Guid.NewGuid(),
            UserId = input.UserId,
            SourceEventId = input.SourceEventId,
            Category = category,
            Delta = delta,
            SourceParticipationId = input.SourceParticipationId ?? source?.SourceParticipationId,
            SourceSubmissionId = input.SourceSubmissionId ?? source?.SourceSubmissionId,
            SourceFindingId = input.SourceFindingId ?? source?.SourceFindingId,
            SourceProjectId = input.SourceProjectId ?? source?.SourceProjectId,
            SourceTaskId = input.SourceTaskId ?? source?.SourceTaskId,
            OccurredAt = input.OccurredAt,
            Note = LabelText(category),
        };

        await store.AddEventAsync(reputationEvent, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        if (category == ReputationCategory.AcceptedContribution)
        {
            await LinkPendingReversalAsync(input, reputationEvent, cancellationToken);
        }

        return await WriteProjectionAsync(input.UserId, clock.UtcNow, cancellationToken);
    }

    /// <summary>
    /// The accepted line a reversal corrects. The seat is looked up first and
    /// the evidence of the seat second, so a reversal that only knows the
    /// evidence still finds its source.
    /// </summary>
    private async Task<ReputationEvent?> FindAcceptedSourceAsync(
        ReputationEventInput input,
        CancellationToken cancellationToken)
    {
        if (input.SourceParticipationId is null && input.SourceSubmissionId is null)
        {
            return null;
        }

        var events = await store.ListEventsAsync(input.UserId, cancellationToken);

        var accepted = events
            .Where(reputationEvent => reputationEvent.Category == ReputationCategory.AcceptedContribution)
            .ToArray();

        if (input.SourceParticipationId is Guid participationId)
        {
            var byParticipation = accepted.FirstOrDefault(
                reputationEvent => reputationEvent.SourceParticipationId == participationId);

            if (byParticipation is not null)
            {
                return byParticipation;
            }
        }

        if (input.SourceSubmissionId is Guid submissionId)
        {
            return accepted.FirstOrDefault(
                reputationEvent => reputationEvent.SourceSubmissionId == submissionId);
        }

        return null;
    }

    /// <summary>
    /// A reversal that arrived before its acceptance takes the source of that
    /// acceptance, so both lines describe the same source once the
    /// acceptance lands.
    /// </summary>
    private async Task LinkPendingReversalAsync(
        ReputationEventInput input,
        ReputationEvent accepted,
        CancellationToken cancellationToken)
    {
        if (input.SourceParticipationId is null && input.SourceSubmissionId is null)
        {
            return;
        }

        var events = await store.ListEventsAsync(input.UserId, cancellationToken);

        var pending = events.FirstOrDefault(reputationEvent =>
            reputationEvent.Category == ReputationCategory.ReversedContribution &&
            reputationEvent.SourceProjectId is null &&
            ((input.SourceParticipationId is Guid participationId &&
                reputationEvent.SourceParticipationId == participationId) ||
             (input.SourceSubmissionId is Guid submissionId &&
                reputationEvent.SourceSubmissionId == submissionId)));

        if (pending is null)
        {
            return;
        }

        pending.SourceProjectId = accepted.SourceProjectId;
        pending.SourceTaskId = accepted.SourceTaskId ?? pending.SourceTaskId;
        pending.SourceParticipationId = accepted.SourceParticipationId ?? pending.SourceParticipationId;

        await store.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the score of the contributor. The score is the sum of the deltas
    /// of the stored events, so a reversal that arrived before its acceptance
    /// is corrected by the acceptance itself. A snapshot is appended whenever
    /// the score moves.
    /// </summary>
    private async Task<ReputationApplyResult> WriteProjectionAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var events = await store.ListEventsAsync(userId, cancellationToken);

        var score = events.Sum(reputationEvent => reputationEvent.Delta);
        var accepted = events.Count(reputationEvent => reputationEvent.Category == ReputationCategory.AcceptedContribution);
        var confirmed = events.Count(reputationEvent => reputationEvent.Category == ReputationCategory.ConfirmedFinding);
        var reversed = events.Count(reputationEvent => reputationEvent.Category == ReputationCategory.ReversedContribution);

        var row = await store.FindScoreAsync(userId, cancellationToken);

        if (row is null)
        {
            row = new ReputationScore { UserId = userId };
            await store.AddScoreAsync(row, cancellationToken);
        }

        if (row.CurrentScore != score)
        {
            await store.AddSnapshotAsync(new ReputationSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentScore = score,
                AcceptedContributions = accepted,
                ConfirmedFindings = confirmed,
                ReversedContributions = reversed,
                TakenAt = now,
            }, cancellationToken);
        }

        row.CurrentScore = score;
        row.AcceptedContributions = accepted;
        row.ConfirmedFindings = confirmed;
        row.ReversedContributions = reversed;
        row.UpdatedAt = now;

        await store.SaveChangesAsync(cancellationToken);

        return new ReputationApplyResult(
            ReputationApplyStatus.Succeeded,
            score,
            accepted,
            confirmed,
            reversed);
    }

    private async Task<ReputationApplyResult> ReadResultAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await store.FindScoreAsync(userId, cancellationToken);

        return new ReputationApplyResult(
            ReputationApplyStatus.Duplicate,
            row?.CurrentScore ?? 0,
            row?.AcceptedContributions ?? 0,
            row?.ConfirmedFindings ?? 0,
            row?.ReversedContributions ?? 0);
    }

    private async Task<ReputationHistoryView> BuildHistoryAsync(
        Guid userId,
        string username,
        List<ReputationEventView> views,
        CancellationToken cancellationToken)
    {
        var score = await store.FindScoreAsync(userId, cancellationToken);

        return new ReputationHistoryView(
            username,
            score?.CurrentScore ?? 0,
            score?.AcceptedContributions ?? 0,
            score?.ConfirmedFindings ?? 0,
            score?.ReversedContributions ?? 0,
            score?.UpdatedAt,
            views);
    }

    /// <summary>
    /// The reference of the source of a line, when the reader may read it. A
    /// reader that may not read the project never receives its title.
    /// </summary>
    private async Task<ProjectReferenceView?> ReadReferenceAsync(
        ProjectActor actor,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (projectId is not Guid id)
        {
            return null;
        }

        return await projects.GetReferenceAsync(actor, id, cancellationToken);
    }
}
