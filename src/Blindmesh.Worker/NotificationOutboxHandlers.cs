using System.Text.Json;
using Blindmesh.Application.Findings;
using Blindmesh.Application.Moderation;
using Blindmesh.Application.Notifications;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Notifications;
using Blindmesh.Domain.Shared;

namespace Blindmesh.Worker;

/// <summary>
/// Turns one workflow event into the notifications of its parties. The
/// handler only names the event and its parties; the notification module owns
/// the queue, the summary and the deep link. The identity of a notification is
/// the event type of the notification and the id of the outbox message, so a
/// redelivered message never writes a second notification.
/// </summary>
public abstract class NotificationOutboxHandler(NotificationService notifications) : IOutboxHandler
{
    /// <summary>The outbox event the handler consumes.</summary>
    public abstract string EventType { get; }

    /// <summary>The event type of the notification the event produces.</summary>
    protected abstract string NotificationEventType { get; }

    public abstract Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>Writes the notification of one party of the event.</summary>
    protected Task NotifyAsync(
        OutboxMessage message,
        NotificationCategory category,
        Guid recipientId,
        string deepLink,
        CancellationToken cancellationToken) =>
        notifications.EnqueueAsync(
            NotificationEventTypes.Identity(NotificationEventType, message.Id),
            category,
            recipientId,
            deepLink,
            cancellationToken);
}

/// <summary>
/// The owner of a task is told that a contributor applied for one of its
/// seats.
/// </summary>
public sealed class ApplicationReceivedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => TasksEventTypes.ApplicationReceived;

    protected override string NotificationEventType => NotificationEventTypes.ApplicationReceived;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TasksEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await NotifyAsync(
            message,
            NotificationCategory.ApplicationReceived,
            payload.PublisherId,
            $"/tasks/{payload.TaskId:D}",
            cancellationToken);
    }
}

/// <summary>
/// The contributor is told that the owner of the task accepted the seat.
/// </summary>
public sealed class ApplicationAcceptedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => TasksEventTypes.ApplicationAccepted;

    protected override string NotificationEventType => NotificationEventTypes.ApplicationAccepted;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TasksEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await NotifyAsync(
            message,
            NotificationCategory.ApplicationAccepted,
            payload.ContributorId,
            $"/work/{payload.ParticipationId:D}",
            cancellationToken);
    }
}

/// <summary>
/// The owner of a task is told that the evidence of a seat arrived.
/// </summary>
public sealed class SubmissionReceivedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => FindingsEventTypes.SubmissionReceived;

    protected override string NotificationEventType => NotificationEventTypes.SubmissionReceived;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FindingsEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await NotifyAsync(
            message,
            NotificationCategory.SubmissionReceived,
            payload.PublisherId,
            $"/work/{payload.ParticipationId:D}",
            cancellationToken);
    }
}

/// <summary>
/// The contributor is told that the owner of the task asked for a correction.
/// </summary>
public sealed class CorrectionRequestedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => FindingsEventTypes.CorrectionRequested;

    protected override string NotificationEventType => NotificationEventTypes.CorrectionRequested;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FindingsEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await NotifyAsync(
            message,
            NotificationCategory.CorrectionRequested,
            payload.ContributorId,
            $"/work/{payload.ParticipationId:D}",
            cancellationToken);
    }
}

/// <summary>
/// The contributor is told that the work of the seat was accepted.
/// </summary>
public sealed class ParticipationAcceptedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => FindingsEventTypes.ParticipationAccepted;

    protected override string NotificationEventType => NotificationEventTypes.Acceptance;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FindingsEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await NotifyAsync(
            message,
            NotificationCategory.Acceptance,
            payload.ContributorId,
            $"/work/{payload.ParticipationId:D}",
            cancellationToken);
    }
}

/// <summary>
/// The contributor is told that a finding of the contributor was confirmed.
/// </summary>
public sealed class FindingConfirmedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => FindingsEventTypes.FindingConfirmed;

    protected override string NotificationEventType => NotificationEventTypes.ModerationOutcome;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FindingsEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        var deepLink = payload.FindingId is Guid findingId && findingId != Guid.Empty
            ? $"/findings/{findingId:D}"
            : $"/tasks/{payload.TaskId:D}";

        await NotifyAsync(
            message,
            NotificationCategory.ModerationOutcome,
            payload.ContributorId,
            deepLink,
            cancellationToken);
    }
}

/// <summary>
/// The parties of a case of arbitration are told that the case changed.
/// </summary>
public abstract class DisputeNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    protected override string NotificationEventType => NotificationEventTypes.DisputeUpdate;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ModerationEventPayload>(message.Payload);

        if (payload is null || payload.DisputeId is not Guid disputeId)
        {
            return;
        }

        var deepLink = $"/disputes/{disputeId:D}";

        if (payload.PublisherId is Guid publisherId && publisherId != Guid.Empty)
        {
            await NotifyAsync(
                message,
                NotificationCategory.DisputeUpdate,
                publisherId,
                deepLink,
                cancellationToken);
        }

        if (payload.ContributorId is Guid contributorId && contributorId != Guid.Empty)
        {
            await NotifyAsync(
                message,
                NotificationCategory.DisputeUpdate,
                contributorId,
                deepLink,
                cancellationToken);
        }
    }
}

/// <summary>The parties of a case are told that the case was opened.</summary>
public sealed class DisputeOpenedNotificationHandler(NotificationService notifications)
    : DisputeNotificationHandler(notifications)
{
    public override string EventType => ModerationEventTypes.DisputeOpened;
}

/// <summary>The parties of a case are told that the case was decided.</summary>
public sealed class DisputeResolvedNotificationHandler(NotificationService notifications)
    : DisputeNotificationHandler(notifications)
{
    public override string EventType => ModerationEventTypes.DisputeResolved;
}

/// <summary>The parties of a case are told that the decision was appealed.</summary>
public sealed class DisputeAppealedNotificationHandler(NotificationService notifications)
    : DisputeNotificationHandler(notifications)
{
    public override string EventType => ModerationEventTypes.DisputeAppealed;
}

/// <summary>
/// The parties of a case are told that the arbitration changed the outcome of
/// the seat of the case.
/// </summary>
public sealed class ArbitrationCompensatedNotificationHandler(NotificationService notifications)
    : NotificationOutboxHandler(notifications)
{
    public override string EventType => ModerationEventTypes.ArbitrationCompensated;

    protected override string NotificationEventType => NotificationEventTypes.ModerationOutcome;

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ModerationEventPayload>(message.Payload);

        if (payload is null || payload.DisputeId is not Guid disputeId)
        {
            return;
        }

        var deepLink = $"/disputes/{disputeId:D}";

        if (payload.PublisherId is Guid publisherId && publisherId != Guid.Empty)
        {
            await NotifyAsync(
                message,
                NotificationCategory.ModerationOutcome,
                publisherId,
                deepLink,
                cancellationToken);
        }

        if (payload.ContributorId is Guid contributorId && contributorId != Guid.Empty)
        {
            await NotifyAsync(
                message,
                NotificationCategory.ModerationOutcome,
                contributorId,
                deepLink,
                cancellationToken);
        }
    }
}
