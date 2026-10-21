using System.Text.Json;
using Blindmesh.Application.Findings;
using Blindmesh.Application.Reputation;
using Blindmesh.Domain.Shared;

namespace Blindmesh.Worker;

/// <summary>
/// Applies the acceptance of a seat to the reputation of the contributor. The
/// handler shares the scoped database context of the cycle, so the line of the
/// history and the score are written in the transaction of the cycle.
/// </summary>
public sealed class ParticipationAcceptedReputationHandler(ReputationService reputation) : IOutboxHandler
{
    public string EventType => FindingsEventTypes.ParticipationAccepted;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FindingsEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await reputation.ApplyAcceptedAsync(
            new ReputationEventInput(
                message.Id,
                payload.ContributorId,
                payload.ParticipationId == Guid.Empty ? null : payload.ParticipationId,
                payload.SubmissionId,
                null,
                payload.ProjectId,
                payload.TaskId,
                payload.OccurredAt),
            cancellationToken);
    }
}
