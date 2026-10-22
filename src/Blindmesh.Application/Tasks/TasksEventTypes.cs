namespace Blindmesh.Application.Tasks;

/// <summary>
/// The outbox event types owned by the tasks package. The package writes them
/// inside the transaction of its own command, so an event is never stored
/// without the state change that produced it. The consumers re-check the
/// access to the source of the event.
/// </summary>
public static class TasksEventTypes
{
    /// <summary>The owner of a task received an application for one of its seats.</summary>
    public const string ApplicationReceived = "tasks.application.received";

    /// <summary>The owner of a task accepted an application for one of its seats.</summary>
    public const string ApplicationAccepted = "tasks.application.accepted";
}

/// <summary>
/// The payload of the outbox events of the tasks package. It names the seat
/// and the parties of the event; it never holds the title of a task, the scope
/// of a task or a link to a file.
/// </summary>
public sealed record TasksEventPayload(
    Guid ParticipationId,
    Guid TaskId,
    Guid ProjectId,
    Guid PublisherId,
    Guid ContributorId,
    DateTimeOffset OccurredAt);
