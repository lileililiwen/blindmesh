namespace Blindmesh.Domain.Reputation;

/// <summary>
/// The score of a contributor. The row is a projection of the stored events:
/// the score is the sum of the deltas of the events of the contributor. It is
/// written in the same transaction as the event that changed it.
/// </summary>
public sealed class ReputationScore
{
    /// <summary>The contributor the score belongs to. It is the key of the row.</summary>
    public Guid UserId { get; set; }

    public int AcceptedContributions { get; set; }

    public int ConfirmedFindings { get; set; }

    public int ReversedContributions { get; set; }

    public int CurrentScore { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
