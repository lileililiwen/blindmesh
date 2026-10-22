using Blindmesh.Application.Findings;
using Blindmesh.Application.Moderation;
using Blindmesh.Application.Notifications;
using Blindmesh.Application.Projects;
using Blindmesh.Application.Tasks;

namespace Blindmesh.Infrastructure.Notifications;

/// <summary>
/// The access reader of the notification module. It reads the source of a
/// notification again and answers whether the recipient may still read it. The
/// answer is read when the message is sent, so a revoked membership never
/// leaves the private source of a notification in the mailbox of the
/// recipient.
/// </summary>
public sealed class NotificationAccessReader(
    IParticipationStore participations,
    ITaskStore tasks,
    IModerationStore moderation,
    IFindingsStore findings,
    ProjectService projects) : INotificationAccessReader
{
    public async Task<bool> MayReadSourceAsync(
        Guid recipientId,
        string deepLink,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deepLink))
        {
            return false;
        }

        var segments = deepLink.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2 || !Guid.TryParse(segments[1], out var id))
        {
            // A deep link that points at nothing the product knows is never
            // readable.
            return false;
        }

        return segments[0] switch
        {
            "work" or "participations" => await MayReadParticipationAsync(recipientId, id, cancellationToken),
            "tasks" => await MayReadTaskAsync(recipientId, id, cancellationToken),
            "projects" => await MayReadProjectAsync(recipientId, id, cancellationToken),
            "disputes" => await MayReadDisputeAsync(recipientId, id, cancellationToken),
            "findings" => await MayReadFindingAsync(recipientId, id, cancellationToken),
            _ => false,
        };
    }

    private async Task<bool> MayReadParticipationAsync(
        Guid recipientId,
        Guid participationId,
        CancellationToken cancellationToken)
    {
        var participation = await participations.FindParticipationAsync(participationId, cancellationToken);

        if (participation is null)
        {
            return false;
        }

        var task = await tasks.FindTaskAsync(participation.TaskId, cancellationToken);

        if (task is null)
        {
            return false;
        }

        if (task.OwnerId == recipientId)
        {
            return true;
        }

        if (participation.ContributorId != recipientId)
        {
            return false;
        }

        // The contributor may read the seat only while the project of the seat
        // is readable for them. A revoked membership takes the source away.
        var access = await projects.GetAccessAsync(
            new ProjectActor(recipientId, string.Empty),
            task.ProjectId,
            cancellationToken);

        return access is not null;
    }

    private async Task<bool> MayReadTaskAsync(
        Guid recipientId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await tasks.FindTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return false;
        }

        if (task.OwnerId == recipientId)
        {
            return true;
        }

        var access = await projects.GetAccessAsync(
            new ProjectActor(recipientId, string.Empty),
            task.ProjectId,
            cancellationToken);

        return access is not null;
    }

    private async Task<bool> MayReadProjectAsync(
        Guid recipientId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await projects.GetAccessAsync(
            new ProjectActor(recipientId, string.Empty),
            projectId,
            cancellationToken);

        return access is not null;
    }

    private async Task<bool> MayReadFindingAsync(
        Guid recipientId,
        Guid findingId,
        CancellationToken cancellationToken)
    {
        var finding = await findings.FindFindingAsync(findingId, cancellationToken);

        if (finding is null)
        {
            return false;
        }

        if (finding.ContributorId == recipientId)
        {
            return true;
        }

        var submission = await findings.FindSubmissionAsync(finding.SubmissionId, cancellationToken);

        if (submission is null)
        {
            return false;
        }

        var task = await tasks.FindTaskAsync(submission.TaskId, cancellationToken);

        return task is not null && task.OwnerId == recipientId;
    }

    private async Task<bool> MayReadDisputeAsync(
        Guid recipientId,
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var dispute = await moderation.FindDisputeAsync(disputeId, cancellationToken);

        if (dispute is null)
        {
            return false;
        }

        return dispute.PublisherId == recipientId || dispute.ContributorId == recipientId;
    }
}
