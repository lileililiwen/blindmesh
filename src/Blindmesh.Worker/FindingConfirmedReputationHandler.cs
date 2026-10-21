using System.Text.Json;
using Blindmesh.Application.Findings;
using Blindmesh.Application.Reputation;
using Blindmesh.Domain.Shared;

namespace Blindmesh.Worker;

/// <summary>
/// Applies the confirmation of a finding to the reputation of the contributor
/// who raised it. The handler shares the scoped database context of the cycle,
/// so the line of the history and the score are written in the transaction of
/// the cycle.
/// </summary>
public sealed class FindingConfirmedReputationHandler(ReputationService reputation) : IOutboxHandler
{
    public string EventType => FindingsEventTypes.FindingConfirmed;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FindingsEventPayload>(message.Payload);

        if (payload is null)
        {
            return;
        }

        await reputation.ApplyFindingConfirmedAsync(
            new ReputationEventInput(
                message.Id,
                payload.ContributorId,
                null,
                payload.SubmissionId,
                payload.FindingId,
                payload.ProjectId,
                payload.TaskId,
                payload.OccurredAt),
            cancellationToken);
    }
}
