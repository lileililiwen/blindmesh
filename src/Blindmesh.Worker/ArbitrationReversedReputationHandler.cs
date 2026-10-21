using System.Text.Json;
using Blindmesh.Application.Moderation;
using Blindmesh.Application.Reputation;
using Blindmesh.Domain.Shared;

namespace Blindmesh.Worker;

/// <summary>
/// Applies the reversal of an arbitration to the reputation of the contributor.
/// The source of the reversal is the seat of the case, so the line of the
/// history names the same source as the acceptance it corrects. The handler
/// shares the scoped database context of the cycle, so the line of the history
/// and the score are written in the transaction of the cycle.
/// </summary>
public sealed class ArbitrationReversedReputationHandler(ReputationService reputation) : IOutboxHandler
{
    public string EventType => ModerationEventTypes.ArbitrationReversed;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ModerationEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await reputation.ApplyReversedAsync(
            new ReputationEventInput(
                message.Id,
                payload.ContributorId ?? Guid.Empty,
                payload.ParticipationId,
                payload.SubmissionId,
                null,
                null,
                payload.TaskId,
                payload.OccurredAt),
            cancellationToken);
    }
}
