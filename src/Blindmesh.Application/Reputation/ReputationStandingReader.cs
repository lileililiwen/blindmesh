namespace Blindmesh.Application.Reputation;

/// <summary>
/// The reader of the reputation module that answers the matching module. It
/// reads the score of an account and never writes a row, so the order of the
/// candidate list stays a read. It is a reader of its own module and not the
/// reputation service, so the matching module never depends on a service that
/// reaches the project module: no seam creates a cycle at runtime.
/// </summary>
public sealed class ReputationStandingReader(IReputationStore store) : IReputationStandingReader
{
    public async Task<int> AcceptedContributionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var score = await store.FindScoreAsync(userId, cancellationToken);

        return score?.AcceptedContributions ?? 0;
    }
}
