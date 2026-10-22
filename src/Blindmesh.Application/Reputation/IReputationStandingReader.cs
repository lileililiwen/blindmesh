namespace Blindmesh.Application.Reputation;

/// <summary>
/// The seam of the reputation module for the matching module. The reputation
/// module owns the accepted contributions of an account, so it exposes their
/// number. An account the reputation module does not know has zero accepted
/// contributions, so it is an explorer in the order of the candidate list.
/// </summary>
public interface IReputationStandingReader
{
    /// <summary>The number of the accepted contributions of an account.</summary>
    Task<int> AcceptedContributionsAsync(Guid userId, CancellationToken cancellationToken);
}
